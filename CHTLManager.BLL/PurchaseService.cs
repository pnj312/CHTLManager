using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL;

/// <summary>Nhà cung cấp và phiếu nhập hàng.</summary>
public class PurchaseService : IPurchaseService
{
    private readonly Func<AppDbContext> _factory;

    public PurchaseService() : this(() => new AppDbContext())
    {
    }

    public PurchaseService(Func<AppDbContext> factory)
    {
        _factory = factory;
    }

    public Supplier SaveSupplier(Supplier supplier)
    {
        string name = supplier.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new BusinessException("Hãy nhập tên nhà cung cấp.");
        string key = name.ToLowerInvariant();

        using var db = _factory();
        var existing = db.Suppliers.FirstOrDefault(s => s.NameKey == key);
        if (existing != null) return existing;

        var entity = new Supplier { Name = name, NameKey = key, Phone = supplier.Phone?.Trim(), IsActive = true };
        db.Suppliers.Add(entity);
        db.SaveChanges();
        return entity;
    }

    public List<Supplier> GetSuppliers()
    {
        using var db = _factory();
        return db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
    }

    public PurchaseReceipt CreateReceipt(int supplierId, string invoiceNo, IReadOnlyList<ReceiptLineInput> lines, int userId, bool asAdmin)
    {
        string invoice = invoiceNo?.Trim() ?? string.Empty;
        if (invoice.Length == 0)
            throw new BusinessException("Hãy nhập số hóa đơn của nhà cung cấp.");
        if (lines == null || lines.Count == 0)
            throw new BusinessException("Phiếu nhập chưa có sản phẩm nào.");
        if (lines.Select(l => l.ProductId).Distinct().Count() != lines.Count)
            throw new BusinessException("Một sản phẩm chỉ được nằm trên một dòng của phiếu.");
        if (lines.Any(l => l.InvoicedQuantity <= 0 || l.ReceivedQuantity < 0))
            throw new BusinessException("Số lượng theo hóa đơn phải lớn hơn 0 và số thực nhận không được âm.");
        if (lines.All(l => l.ReceivedQuantity == 0))
            throw new BusinessException("Phiếu nhập không có hàng thực nhận.");
        if (asAdmin && lines.Any(l => l.UnitCost == null))
            throw new BusinessException("Hãy nhập giá nhập cho mọi dòng.");
        if (lines.Any(l => l.UnitCost < 0))
            throw new BusinessException("Giá nhập không được âm.");

        string key = invoice.ToUpperInvariant();
        using var db = _factory();
        var supplier = db.Suppliers.FirstOrDefault(s => s.Id == supplierId && s.IsActive)
                       ?? throw new BusinessException("Không tìm thấy nhà cung cấp.");

        if (db.PurchaseReceipts.Any(r => r.SupplierId == supplierId && r.InvoiceKey == key && r.Status != ReceiptStatus.Cancelled))
            throw new BusinessException($"Hóa đơn '{invoice}' của {supplier.Name} đã được nhập rồi.");

        var ids = lines.Select(l => l.ProductId).ToList();
        var products = db.Products.Where(p => ids.Contains(p.Id) && p.IsActive).ToDictionary(p => p.Id);
        if (products.Count != lines.Count)
            throw new BusinessException("Có sản phẩm không tồn tại hoặc đã ngừng kinh doanh.");

        var outOfBook = products.Values.FirstOrDefault(p => p.Stock < 0);
        if (outOfBook != null)
            throw new BusinessException($"'{outOfBook.Name}' đã bán {-outOfBook.Stock} cái ngoài sổ từ lô trước. Hãy kiểm kê trước khi nhập lô mới.");

        DateTime now = DateTime.Now;
        DateTime startOfDay = now.Date;
        int countToday = db.PurchaseReceipts.Count(r => r.ReceivedAt >= startOfDay);

        var receipt = new PurchaseReceipt
        {
            Code = $"PN{now:yyyyMMdd}-{countToday + 1:D4}",
            SupplierId = supplierId,
            Supplier = supplier,
            InvoiceNo = invoice,
            InvoiceKey = key,
            ReceivedAt = now,
            CreatedByUserId = userId,
            Status = asAdmin ? ReceiptStatus.Confirmed : ReceiptStatus.Pending,
            ConfirmedByUserId = asAdmin ? userId : null,
            ConfirmedAt = asAdmin ? now : null,
            Lines = lines.Select(l => new PurchaseReceiptLine
            {
                ProductId = l.ProductId,
                InvoicedQuantity = l.InvoicedQuantity,
                ReceivedQuantity = l.ReceivedQuantity,
                UnitCost = asAdmin ? l.UnitCost : null
            }).ToList()
        };
        db.PurchaseReceipts.Add(receipt);

        foreach (var line in receipt.Lines)
        {
            var product = products[line.ProductId];
            if (asAdmin) ApplyCost(product, product.Stock, line);
            StockLedger.Apply(db, product, product.Stock + line.ReceivedQuantity, StockMovementType.ReceiptIn, userId,
                supplier: supplier.Name, invoiceNo: invoice, receipt: receipt);
        }

        Save(db);
        return receipt;
    }

    public PurchaseReceipt ConfirmReceipt(int receiptId, IReadOnlyDictionary<int, decimal> unitCostByLineId, int adminId)
    {
        using var db = _factory();
        var receipt = db.PurchaseReceipts.Include(r => r.Lines).ThenInclude(l => l.Product).FirstOrDefault(r => r.Id == receiptId)
                      ?? throw new BusinessException("Không tìm thấy phiếu nhập.");
        if (receipt.Status != ReceiptStatus.Pending)
            throw new BusinessException($"Phiếu {receipt.Code} không ở trạng thái chờ xác nhận.");

        foreach (var line in receipt.Lines)
        {
            if (!unitCostByLineId.TryGetValue(line.Id, out decimal cost))
                throw new BusinessException("Hãy nhập giá nhập cho mọi dòng.");
            if (cost < 0)
                throw new BusinessException("Giá nhập không được âm.");
            line.UnitCost = cost;

            // Phần tồn ngoài phiếu này (đã cộng lúc nhận hàng) mới là hàng cũ tính bình quân.
            ApplyCost(line.Product!, line.Product!.Stock - line.ReceivedQuantity, line);
        }

        receipt.Status = ReceiptStatus.Confirmed;
        receipt.ConfirmedByUserId = adminId;
        receipt.ConfirmedAt = DateTime.Now;
        Save(db);
        return receipt;
    }

    public void CancelReceipt(int receiptId, string reason, int adminId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessException("Hãy nhập lý do hủy phiếu.");

        using var db = _factory();
        var receipt = db.PurchaseReceipts
                          .Include(r => r.Supplier)
                          .Include(r => r.Lines).ThenInclude(l => l.Product)
                          .FirstOrDefault(r => r.Id == receiptId)
                      ?? throw new BusinessException("Không tìm thấy phiếu nhập.");
        if (receipt.Status == ReceiptStatus.Cancelled)
            throw new BusinessException($"Phiếu {receipt.Code} đã hủy rồi.");

        bool costWasApplied = receipt.Status == ReceiptStatus.Confirmed;
        receipt.Status = ReceiptStatus.Cancelled;
        receipt.CancelReason = reason.Trim();
        foreach (var line in receipt.Lines)
        {
            if (costWasApplied)
                line.Product!.AverageCost = CostCalculator.RemoveFromAverage(line.Product.Stock, line.Product.AverageCost, line.ReceivedQuantity, line.UnitCost ?? 0);

            StockLedger.Apply(db, line.Product!, line.Product!.Stock - line.ReceivedQuantity, StockMovementType.ReceiptCancelled, adminId,
                supplier: receipt.Supplier!.Name, invoiceNo: receipt.InvoiceNo, receipt: receipt);
        }
        Save(db);
    }

    public List<PurchaseReceipt> GetReceipts(DateTime from, DateTime to, ReceiptStatus? status)
    {
        DateTime start = from.Date, endExclusive = to.Date.AddDays(1);
        if (start >= endExclusive)
            throw new BusinessException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        using var db = _factory();
        IQueryable<PurchaseReceipt> query = db.PurchaseReceipts
            .AsNoTracking()
            .Include(r => r.Supplier)
            .Include(r => r.CreatedBy)
            .Include(r => r.ConfirmedBy)
            .Include(r => r.Lines).ThenInclude(l => l.Product)
            .Where(r => r.ReceivedAt >= start && r.ReceivedAt < endExclusive);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        return query.OrderByDescending(r => r.ReceivedAt).ThenByDescending(r => r.Id).ToList();
    }

    public int CountPendingReceipts()
    {
        using var db = _factory();
        return db.PurchaseReceipts.Count(r => r.Status == ReceiptStatus.Pending);
    }

    private static void ApplyCost(Product product, int stockBefore, PurchaseReceiptLine line)
    {
        decimal cost = line.UnitCost ?? 0;
        product.AverageCost = CostCalculator.AddToAverage(stockBefore, product.AverageCost, line.ReceivedQuantity, cost);
        if (cost > 0) product.LastCost = cost; // hàng tặng giá 0 không làm mất giá nhập gần nhất
    }

    private static void Save(AppDbContext db)
    {
        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new BusinessException("Không lưu được phiếu nhập do dữ liệu vừa thay đổi hoặc trùng hóa đơn. Vui lòng thử lại.");
        }
    }
}
