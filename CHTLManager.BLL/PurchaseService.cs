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

        string key = InvoiceKeyOf(invoice);
        using var db = _factory();
        var supplier = db.Suppliers.FirstOrDefault(s => s.Id == supplierId && s.IsActive)
                       ?? throw new BusinessException("Không tìm thấy nhà cung cấp.");

        if (InvoiceAlreadyEntered(db, supplierId, key))
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
            if (asAdmin) ApplyCost(product, product.Stock - PendingQuantity(db, product.Id), line);
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

            // Tồn kho đã gồm hàng của phiếu này và các phiếu chờ khác; chúng chưa có giá nên không tính là hàng cũ.
            ApplyCost(line.Product!, line.Product!.Stock - line.ReceivedQuantity - PendingQuantity(db, line.ProductId, receipt.Id), line);
        }

        receipt.Status = ReceiptStatus.Confirmed;
        receipt.ConfirmedByUserId = adminId;
        receipt.ConfirmedAt = DateTime.Now;
        Save(db);
        return receipt;
    }

    public void CorrectReceiptCosts(int receiptId, IReadOnlyDictionary<int, decimal> unitCostByLineId, string reason, int adminId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessException("Hãy nhập lý do sửa giá.");

        using var db = _factory();
        var receipt = db.PurchaseReceipts
                          .Include(r => r.Supplier)
                          .Include(r => r.Lines).ThenInclude(l => l.Product)
                          .FirstOrDefault(r => r.Id == receiptId)
                      ?? throw new BusinessException("Không tìm thấy phiếu nhập.");
        if (receipt.Status != ReceiptStatus.Confirmed)
            throw new BusinessException($"Chỉ sửa giá được phiếu đã xác nhận; phiếu {receipt.Code} đang là '{receipt.StatusName}'.");

        foreach (var line in receipt.Lines)
        {
            if (!unitCostByLineId.TryGetValue(line.Id, out decimal cost))
                throw new BusinessException("Hãy nhập giá nhập cho mọi dòng.");
            if (cost < 0)
                throw new BusinessException("Giá nhập không được âm.");
        }

        decimal newTotal = receipt.Lines.Sum(l => unitCostByLineId[l.Id] * l.ReceivedQuantity);
        decimal paid = PaidTo(db, receipt.SupplierId);
        if (paid > PurchasedFrom(db, receipt.SupplierId) - receipt.Total + newTotal)
            throw new BusinessException(
                $"Không sửa được: đã trả {receipt.Supplier!.Name} {paid:N0} đ, giá mới làm số đã trả vượt tiền hàng. Hãy hủy lần trả tương ứng trước.");

        bool changed = false;
        foreach (var line in receipt.Lines)
        {
            decimal oldCost = line.UnitCost ?? 0, newCost = unitCostByLineId[line.Id];
            if (oldCost == newCost) continue;
            changed = true;

            var product = line.Product!;
            var later = LinesAfter(db, receipt, line.ProductId).Select(x => LotOf(db, x.Receipt, x.Line)).ToList();
            product.AverageCost = CostCalculator.RecalculateLot(product.AverageCost, LotOf(db, receipt, line), later, newCost);
            if (newCost > 0 && !OtherCosts(db, line.ProductId, receipt.Id).Any(c => c.At > CostAppliedAt(receipt)))
                product.LastCost = newCost;
            line.UnitCost = newCost;

            StockLedger.Apply(db, product, product.Stock, StockMovementType.CostCorrected, adminId, logUnchanged: true,
                supplier: receipt.Supplier!.Name, invoiceNo: receipt.InvoiceNo, receipt: receipt,
                note: $"Giá {oldCost:N0} thành {newCost:N0}: {reason.Trim()}");
        }

        if (!changed)
            throw new BusinessException("Giá nhập không có gì thay đổi.");
        Save(db);
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

        if (receipt.StockDiscrepancyId != null)
            throw new BusinessException($"Phiếu {receipt.Code} sinh ra từ kiểm kê, hàng đã nằm trong tồn kho nên không hủy được.");

        bool costWasApplied = receipt.Status == ReceiptStatus.Confirmed;
        if (costWasApplied)
        {
            decimal paid = PaidTo(db, receipt.SupplierId);
            decimal purchasedWithoutThis = PurchasedFrom(db, receipt.SupplierId) - receipt.Total;
            if (paid > purchasedWithoutThis)
                throw new BusinessException(
                    $"Không hủy được: đã trả {receipt.Supplier!.Name} {paid:N0} đ, hủy phiếu này thì số đã trả vượt tiền hàng. Hãy hủy lần trả tương ứng trước (ví dụ khi được hoàn tiền).");
        }


        receipt.Status = ReceiptStatus.Cancelled;
        receipt.CancelReason = reason.Trim();
        foreach (var line in receipt.Lines)
        {
            if (costWasApplied)
            {
                var product = line.Product!;
                var lot = LotOf(db, receipt, line);
                var laterLines = LinesAfter(db, receipt, line.ProductId);
                var later = laterLines.Select(x => LotOf(db, x.Receipt, x.Line)).ToList();
                product.AverageCost = CostCalculator.RecalculateLot(product.AverageCost, lot, later, null);

                // Lô này biến mất nên các lô sau cũng có ít hàng cũ hơn khi vào bình quân; ghi lại để lần hủy sau tính đúng.
                for (int i = 0; i < laterLines.Count; i++)
                    laterLines[i].Line.CostBaseStock = Math.Max(later[i].Before - lot.Quantity, 0);

                // Giá nhập gần nhất không được giữ giá của phiếu vừa hủy.
                var others = OtherCosts(db, line.ProductId, receipt.Id);
                if (others.Count > 0) product.LastCost = others[0].Cost;
                else if (lot.Before > 0 && product.AverageCost > 0) product.LastCost = product.AverageCost;
            }

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

    /// <summary>
    /// Lô chưa ghi phát hiện khi kiểm kê: sinh phiếu đã xác nhận và công nợ, không cộng kho vì hàng đã vào tồn kho lúc đếm.
    /// Người gọi lưu trong cùng SaveChanges với việc xử lý chênh lệch.
    /// </summary>
    internal static PurchaseReceipt AddReceiptForCountedStock(AppDbContext db, int supplierId, string invoiceNo,
        StockDiscrepancy discrepancy, decimal unitCost, int adminId)
    {
        string invoice = invoiceNo.Trim();
        if (invoice.Length == 0)
            throw new BusinessException("Hãy nhập số hóa đơn của lô chưa ghi.");
        string key = InvoiceKeyOf(invoice);

        var supplier = db.Suppliers.FirstOrDefault(s => s.Id == supplierId)
                       ?? throw new BusinessException("Không tìm thấy nhà cung cấp.");
        if (InvoiceAlreadyEntered(db, supplierId, key))
            throw new BusinessException($"Hóa đơn '{invoice}' của {supplier.Name} đã được nhập rồi.");

        var product = db.Products.First(p => p.Id == discrepancy.ProductId);
        DateTime now = DateTime.Now;
        DateTime startOfDay = now.Date;
        int countToday = db.PurchaseReceipts.Count(r => r.ReceivedAt >= startOfDay);

        var line = new PurchaseReceiptLine
        {
            ProductId = product.Id,
            InvoicedQuantity = discrepancy.Delta,
            ReceivedQuantity = discrepancy.Delta,
            UnitCost = unitCost
        };
        var receipt = new PurchaseReceipt
        {
            Code = $"PN{now:yyyyMMdd}-{countToday + 1:D4}",
            SupplierId = supplierId,
            InvoiceNo = invoice,
            InvoiceKey = key,
            ReceivedAt = now,
            CreatedByUserId = adminId,
            ConfirmedByUserId = adminId,
            ConfirmedAt = now,
            Status = ReceiptStatus.Confirmed,
            StockDiscrepancy = discrepancy,
            Lines = { line }
        };
        db.PurchaseReceipts.Add(receipt);

        // Phần đã bán ngoài sổ không còn trên kệ nên chỉ đổi giá phần thực sự tăng lên; lúc đếm nó đã vào bình quân theo giá cũ.
        int onHandIncrease = discrepancy.CountedStock - Math.Max(discrepancy.BookStock, 0);
        line.CostBaseStock = product.AverageCost > 0 ? Math.Max(discrepancy.BookStock, 0) : 0;
        product.AverageCost = CostCalculator.ReplaceInAverage(product.Stock, product.AverageCost, onHandIncrease, Math.Min(discrepancy.UnitCost, product.AverageCost), unitCost, discrepancy.CountedStock);
        if (unitCost > 0) product.LastCost = unitCost;

        return receipt;
    }

    public List<SupplierBalance> GetSupplierBalances()
    {
        using var db = _factory();
        return db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToList()
            .Select(s => new SupplierBalance(s, PurchasedFrom(db, s.Id), PaidTo(db, s.Id)))
            .ToList();
    }

    public List<SupplierLedgerEntry> GetSupplierHistory(int supplierId)
    {
        using var db = _factory();
        var receipts = db.PurchaseReceipts.AsNoTracking()
            .Include(r => r.Lines)
            .Include(r => r.ConfirmedBy)
            .Where(r => r.SupplierId == supplierId && r.Status == ReceiptStatus.Confirmed)
            .ToList();
        var payments = db.SupplierPayments.AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.SupplierId == supplierId)
            .ToList();

        var events = receipts
            .Select(r => (Date: r.ConfirmedAt ?? r.ReceivedAt, Receipt: (PurchaseReceipt?)r, Payment: (SupplierPayment?)null))
            .Concat(payments.Select(p => (Date: p.PaidAt, Receipt: (PurchaseReceipt?)null, Payment: (SupplierPayment?)p)))
            .OrderBy(e => e.Date);

        decimal balance = 0;
        var entries = new List<SupplierLedgerEntry>();
        foreach (var e in events)
        {
            if (e.Receipt is { } r)
            {
                balance += r.Total;
                entries.Add(new SupplierLedgerEntry(e.Date, $"Phiếu {r.Code}, hóa đơn {r.InvoiceNo}", r.Total, 0, balance,
                    r.ConfirmedBy?.FullName ?? string.Empty, null, false));
            }
            else if (e.Payment is { } p)
            {
                if (!p.IsCancelled) balance -= p.Amount;
                string description = p.IsCancelled
                    ? $"Trả {p.Amount:N0} đ (đã hủy: {p.CancelReason})"
                    : string.IsNullOrEmpty(p.Note) ? "Trả tiền" : $"Trả tiền: {p.Note}";
                entries.Add(new SupplierLedgerEntry(e.Date, description, 0, p.IsCancelled ? 0 : p.Amount, balance,
                    p.User?.FullName ?? string.Empty, p.Id, p.IsCancelled));
            }
        }
        entries.Reverse();
        return entries;
    }

    public SupplierPayment PaySupplier(int supplierId, decimal amount, string? note, int adminId)
    {
        if (amount <= 0)
            throw new BusinessException("Số tiền trả phải lớn hơn 0.");

        using var db = _factory();
        var supplier = db.Suppliers.FirstOrDefault(s => s.Id == supplierId)
                       ?? throw new BusinessException("Không tìm thấy nhà cung cấp.");

        decimal balance = PurchasedFrom(db, supplierId) - PaidTo(db, supplierId);
        if (amount > balance)
            throw new BusinessException($"{supplier.Name} chỉ còn nợ {balance:N0} đ, không thể trả {amount:N0} đ.");

        var payment = new SupplierPayment
        {
            SupplierId = supplierId,
            Amount = amount,
            PaidAt = DateTime.Now,
            UserId = adminId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
        db.SupplierPayments.Add(payment);
        db.SaveChanges();
        return payment;
    }

    public void CancelPayment(int paymentId, string reason, int adminId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessException("Hãy nhập lý do hủy lần trả.");

        using var db = _factory();
        var payment = db.SupplierPayments.FirstOrDefault(p => p.Id == paymentId)
                      ?? throw new BusinessException("Không tìm thấy lần trả tiền.");
        if (payment.IsCancelled)
            throw new BusinessException("Lần trả này đã hủy rồi.");

        payment.IsCancelled = true;
        payment.CancelReason = reason.Trim();
        db.SaveChanges();
    }

    // SQLite không cộng được kiểu decimal trong câu SQL, nên tổng hợp trong bộ nhớ.
    private static decimal PurchasedFrom(AppDbContext db, int supplierId)
        => db.PurchaseReceipts.AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.SupplierId == supplierId && r.Status == ReceiptStatus.Confirmed)
            .ToList()
            .Sum(r => r.Total);

    private static decimal PaidTo(AppDbContext db, int supplierId)
        => db.SupplierPayments.AsNoTracking()
            .Where(p => p.SupplierId == supplierId && !p.IsCancelled)
            .ToList()
            .Sum(p => p.Amount);

    /// <summary>Bỏ mọi khoảng trắng và không phân biệt hoa thường: "hd 001" và "HD001" là cùng một hóa đơn.</summary>
    private static string InvoiceKeyOf(string invoice)
        => string.Concat(invoice.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    // So sánh trong bộ nhớ để cũng nhận ra các phiếu cũ có khóa còn khoảng trắng, không cần sửa dữ liệu đã lưu.
    private static bool InvoiceAlreadyEntered(AppDbContext db, int supplierId, string key)
        => db.PurchaseReceipts.AsNoTracking()
            .Where(r => r.SupplierId == supplierId && r.Status != ReceiptStatus.Cancelled)
            .Select(r => r.InvoiceKey)
            .AsEnumerable()
            .Any(k => InvoiceKeyOf(k) == key);

    /// <summary>Số hàng đã vào kho nhưng phiếu chưa được xác nhận, nên chưa có giá.</summary>
    internal static int PendingQuantity(AppDbContext db, int productId, int exceptReceiptId = 0)
        => db.PurchaseReceipts.AsNoTracking()
            .Where(r => r.Status == ReceiptStatus.Pending && r.Id != exceptReceiptId)
            .SelectMany(r => r.Lines)
            .Where(l => l.ProductId == productId)
            .Sum(l => l.ReceivedQuantity);

    // Phiếu của thu ngân chỉ vào bình quân lúc Admin xác nhận, không phải lúc nhận hàng.
    private static DateTime CostAppliedAt(PurchaseReceipt receipt) => receipt.ConfirmedAt ?? receipt.ReceivedAt;

    private static CostLot LotOf(AppDbContext db, PurchaseReceipt receipt, PurchaseReceiptLine line)
    {
        decimal cost = line.UnitCost ?? 0;
        if (receipt.StockDiscrepancyId is int discrepancyId)
        {
            var d = db.StockDiscrepancies.AsNoTracking().First(x => x.Id == discrepancyId);
            int before = Math.Max(d.BookStock, 0);
            return new CostLot(line.CostBaseStock ?? before, d.CountedStock - before, cost);
        }

        // Dữ liệu cũ chưa lưu số này: lấy từ nhật ký kho lúc nhận hàng.
        int stock = line.CostBaseStock ?? Math.Max(db.StockMovements.AsNoTracking()
            .Where(m => m.PurchaseReceiptId == receipt.Id && m.ProductId == line.ProductId && m.Type == StockMovementType.ReceiptIn)
            .Select(m => m.BeforeStock)
            .FirstOrDefault(), 0);
        return new CostLot(stock, line.ReceivedQuantity, cost);
    }

    /// <summary>Các dòng của sản phẩm đã vào bình quân sau phiếu này, theo thứ tự vào.</summary>
    private static List<(PurchaseReceipt Receipt, PurchaseReceiptLine Line)> LinesAfter(AppDbContext db, PurchaseReceipt receipt, int productId)
        => db.PurchaseReceipts
            .Include(r => r.Lines)
            .Where(r => r.Id != receipt.Id && r.Status == ReceiptStatus.Confirmed && r.Lines.Any(l => l.ProductId == productId))
            .ToList()
            .Where(r => CostAppliedAt(r) > CostAppliedAt(receipt))
            .OrderBy(CostAppliedAt)
            .Select(r => (r, r.Lines.First(l => l.ProductId == productId)))
            .ToList();

    /// <summary>Giá nhập có giá trị ở các phiếu đã xác nhận khác, phiếu vào bình quân sau cùng đứng trước.</summary>
    private static List<(DateTime At, decimal Cost)> OtherCosts(AppDbContext db, int productId, int receiptId)
        => db.PurchaseReceipts.AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.Id != receiptId && r.Status == ReceiptStatus.Confirmed && r.Lines.Any(l => l.ProductId == productId))
            .ToList()
            .Select(r => (At: CostAppliedAt(r), Cost: r.Lines.First(l => l.ProductId == productId).UnitCost ?? 0))
            .Where(x => x.Cost > 0)
            .OrderByDescending(x => x.At)
            .ToList();

    private static void ApplyCost(Product product, int pricedStockBefore, PurchaseReceiptLine line)
    {
        decimal cost = line.UnitCost ?? 0;
        line.CostBaseStock = product.AverageCost > 0 ? Math.Max(pricedStockBefore, 0) : 0;
        product.AverageCost = CostCalculator.AddToAverage(pricedStockBefore, product.AverageCost, line.ReceivedQuantity, cost);
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
