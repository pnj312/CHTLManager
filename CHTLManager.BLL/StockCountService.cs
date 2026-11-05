using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL;

/// <summary>Kiểm kê hai bước: đếm để tồn kho đúng ngay, rồi Admin quyết định phần chênh lệch đi về đâu.</summary>
public class StockCountService : IStockCountService
{
    private readonly Func<AppDbContext> _factory;

    public StockCountService() : this(() => new AppDbContext())
    {
    }

    public StockCountService(Func<AppDbContext> factory)
    {
        _factory = factory;
    }

    public StockDiscrepancy? Count(int productId, int countedStock, int userId)
    {
        if (countedStock < 0)
            throw new BusinessException("Số đếm được phải là số nguyên không âm.");

        using var db = _factory();
        var product = db.Products.FirstOrDefault(p => p.Id == productId && p.IsActive)
                      ?? throw new BusinessException("Không tìm thấy sản phẩm.");

        int book = product.Stock;
        StockLedger.Apply(db, product, countedStock, StockMovementType.StockCount, userId, logUnchanged: true);

        StockDiscrepancy? discrepancy = null;
        if (countedStock != book)
        {
            discrepancy = new StockDiscrepancy
            {
                ProductId = productId,
                CountedByUserId = userId,
                CountedAt = DateTime.Now,
                BookStock = book,
                CountedStock = countedStock,
                Delta = countedStock - book,
                UnitCost = product.AverageCost > 0 ? product.AverageCost : product.LastCost,
                Status = DiscrepancyStatus.Pending
            };
            db.StockDiscrepancies.Add(discrepancy);
        }

        db.SaveChanges();
        return discrepancy;
    }

    public void ResolveShortage(int id, DiscrepancyResolution resolution, string? note, int? responsibleUserId,
        string? responsibleName, decimal? compensationAmount, int adminId)
    {
        using var db = _factory();
        var d = LoadPending(db, id);
        if (!d.IsShortage)
            throw new BusinessException("Đây là chênh lệch thừa, không xử lý như hàng thiếu.");

        switch (resolution)
        {
            case DiscrepancyResolution.NormalLoss:
                break;
            case DiscrepancyResolution.Compensation:
                if (responsibleUserId == null && string.IsNullOrWhiteSpace(responsibleName))
                    throw new BusinessException("Hãy chọn hoặc nhập người chịu trách nhiệm.");
                if (compensationAmount is not > 0)
                    throw new BusinessException("Số tiền bồi thường phải lớn hơn 0.");
                d.ResponsibleUserId = responsibleUserId;
                d.ResponsibleName = responsibleUserId == null ? responsibleName!.Trim() : null;
                d.CompensationAmount = compensationAmount;
                break;
            case DiscrepancyResolution.Miscount:
                RequireNote(note);
                break;
            default:
                throw new BusinessException("Cách xử lý này không dùng cho hàng thiếu.");
        }

        MarkResolved(d, resolution, note, adminId);
        db.SaveChanges();
    }

    public void ResolveSurplus(int id, DiscrepancyResolution resolution, string? note, int? supplierId, string? invoiceNo,
        decimal? unitCost, int adminId)
    {
        using var db = _factory();
        var d = LoadPending(db, id);
        if (d.IsShortage)
            throw new BusinessException("Đây là chênh lệch thiếu, không xử lý như hàng thừa.");

        switch (resolution)
        {
            case DiscrepancyResolution.UnrecordedPurchase:
                if (supplierId == null)
                    throw new BusinessException("Hãy chọn nhà cung cấp của lô chưa ghi.");
                if (unitCost is not >= 0)
                    throw new BusinessException("Hãy nhập giá nhập của lô chưa ghi.");
                PurchaseService.AddReceiptForCountedStock(db, supplierId.Value, invoiceNo ?? string.Empty, d, unitCost.Value, adminId);
                break;
            case DiscrepancyResolution.OtherIncome:
                break;
            case DiscrepancyResolution.Miscount:
                RequireNote(note);
                break;
            default:
                throw new BusinessException("Cách xử lý này không dùng cho hàng thừa.");
        }

        MarkResolved(d, resolution, note, adminId);
        db.SaveChanges();
    }

    public CompensationPayment CollectCompensation(int id, decimal amount, string? note, int adminId)
    {
        if (amount <= 0)
            throw new BusinessException("Số tiền thu phải lớn hơn 0.");

        using var db = _factory();
        var d = db.StockDiscrepancies.Include(x => x.Payments).FirstOrDefault(x => x.Id == id)
                ?? throw new BusinessException("Không tìm thấy phiếu chênh lệch.");
        if (d.Resolution != DiscrepancyResolution.Compensation)
            throw new BusinessException("Phiếu này không có khoản bồi thường.");
        if (amount > d.CompensationRemaining)
            throw new BusinessException($"Chỉ còn phải thu {d.CompensationRemaining:N0} đ.");

        var payment = new CompensationPayment
        {
            StockDiscrepancyId = id,
            Amount = amount,
            PaidAt = DateTime.Now,
            UserId = adminId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
        db.CompensationPayments.Add(payment);
        db.SaveChanges();
        return payment;
    }

    public List<StockDiscrepancy> GetDiscrepancies(DateTime from, DateTime to, DiscrepancyStatus? status)
    {
        DateTime start = from.Date, endExclusive = to.Date.AddDays(1);
        if (start >= endExclusive)
            throw new BusinessException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        using var db = _factory();
        IQueryable<StockDiscrepancy> query = db.StockDiscrepancies
            .AsNoTracking()
            .Include(d => d.Product)
            .Include(d => d.CountedBy)
            .Include(d => d.ResolvedBy)
            .Include(d => d.ResponsibleUser)
            .Include(d => d.Payments)
            .Where(d => d.CountedAt >= start && d.CountedAt < endExclusive);
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);

        return query.OrderByDescending(d => d.CountedAt).ThenByDescending(d => d.Id).ToList();
    }

    public int CountPending()
    {
        using var db = _factory();
        return db.StockDiscrepancies.Count(d => d.Status == DiscrepancyStatus.Pending);
    }

    private static StockDiscrepancy LoadPending(AppDbContext db, int id)
    {
        var d = db.StockDiscrepancies.FirstOrDefault(x => x.Id == id)
                ?? throw new BusinessException("Không tìm thấy phiếu chênh lệch.");
        if (d.Status != DiscrepancyStatus.Pending)
            throw new BusinessException("Phiếu chênh lệch này đã được xử lý rồi.");
        return d;
    }

    private static void RequireNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new BusinessException("Đếm sai phải có ghi chú giải thích, để chủ cửa hàng kiểm tra được.");
    }

    private static void MarkResolved(StockDiscrepancy d, DiscrepancyResolution resolution, string? note, int adminId)
    {
        d.Status = DiscrepancyStatus.Resolved;
        d.Resolution = resolution;
        d.ResolvedByUserId = adminId;
        d.ResolvedAt = DateTime.Now;
        d.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
