using CHTLManager.BLL.Interfaces;
using CHTLManager.BLL.Reports;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL;

/// <summary>Tạo hóa đơn bán hàng, tính tổng tiền/tiền thừa và lưu lịch sử bán.</summary>
public class OrderService : IOrderService
{
    /// <summary>Số cái được bán vượt tồn kho trên một dòng hóa đơn; nhiều hơn thì phải nhận hàng hoặc kiểm kê trước.</summary>
    public const int MaxOutOfBookPerLine = 10;

    private readonly Func<AppDbContext> _factory;

    public OrderService() : this(() => new AppDbContext())
    {
    }

    public OrderService(Func<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Tổng tiền = Σ (đơn giá × số lượng).</summary>
    public static decimal CalculateTotal(IEnumerable<OrderDetail> details)
        => details.Sum(d => d.UnitPrice * d.Quantity);

    /// <summary>Tiền thừa = tiền khách đưa − tổng tiền.</summary>
    public static decimal CalculateChange(decimal total, decimal customerPaid)
        => customerPaid - total;

    public Order CreateOrder(Order order, List<OrderDetail> details, ISet<int>? allowOversellProductIds = null)
    {
        if (order == null) throw new BusinessException("Thông tin hóa đơn không hợp lệ.");
        if (details == null || details.Count == 0)
            throw new BusinessException("Hóa đơn chưa có sản phẩm nào.");

        using var db = _factory();
        using var tx = db.Database.BeginTransaction();

        if (!db.Users.Any(u => u.Id == order.CashierId && u.IsActive))
            throw new BusinessException("Thu ngân không hợp lệ.");

        var ids = details.Select(d => d.ProductId).Distinct().ToList();
        var products = db.Products.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);

        var lines = new List<OrderDetail>();
        var outOfBook = new List<StockMovement>();
        DateTime now = DateTime.Now;
        foreach (var group in details.GroupBy(d => d.ProductId))
        {
            if (!products.TryGetValue(group.Key, out var product) || !product.IsActive)
                throw new BusinessException("Có sản phẩm không tồn tại hoặc đã ngừng kinh doanh.");

            int quantity = group.Sum(d => d.Quantity);
            if (quantity <= 0)
                throw new BusinessException($"Số lượng của '{product.Name}' phải lớn hơn 0.");
            // Kho trên phần mềm có thể lệch thực tế (hàng nhập chưa ghi, đếm sai...). Chỉ cho bán vượt tồn
            // với sản phẩm thu ngân đã xác nhận là còn hàng thật; khi đó tồn kho có thể âm và cần kiểm kê sau.
            bool oversellAllowed = allowOversellProductIds?.Contains(product.Id) == true;
            if (product.Stock < quantity && !oversellAllowed)
                throw new BusinessException($"'{product.Name}' chỉ còn {Math.Max(product.Stock, 0)} trong kho.");

            if (quantity - Math.Max(product.Stock, 0) > MaxOutOfBookPerLine)
                throw new BusinessException(
                    $"'{product.Name}' chỉ được bán ngoài sổ tối đa {MaxOutOfBookPerLine} cái. Hãy nhận hàng hoặc kiểm kê trước khi bán thêm.");

            int before = product.Stock;
            product.Stock -= quantity;
            if (before < quantity)
            {
                outOfBook.Add(new StockMovement
                {
                    ProductId = product.Id,
                    UserId = order.CashierId,
                    CreatedAt = now,
                    Type = StockMovementType.OutOfBookSale,
                    BeforeStock = before,
                    AfterStock = product.Stock,
                    Delta = -quantity,
                    UnitPrice = product.Price
                });
            }
            lines.Add(new OrderDetail
            {
                ProductId = product.Id,
                Product = product,
                Quantity = quantity,
                UnitPrice = product.Price, // luôn lấy giá từ CSDL, không tin giá do giao diện gửi lên
                UnitCost = product.AverageCost > 0 ? product.AverageCost : product.LastCost,
                CostIsEstimated = before < quantity || product.AverageCost <= 0
            });
        }

        decimal total = CalculateTotal(lines);
        if (order.CustomerPaid < total)
            throw new BusinessException($"Tiền khách đưa chưa đủ (cần {total:N0} đ).");

        DateTime startOfDay = now.Date;
        int countToday = db.Orders.Count(o => o.CreatedAt >= startOfDay);

        var newOrder = new Order
        {
            Code = $"HD{now:yyyyMMdd}-{countToday + 1:D4}",
            CreatedAt = now,
            CashierId = order.CashierId,
            Total = total,
            CustomerPaid = order.CustomerPaid,
            Change = CalculateChange(total, order.CustomerPaid),
            Details = lines
        };

        db.Orders.Add(newOrder);
        foreach (var movement in outOfBook) movement.Order = newOrder;
        db.StockMovements.AddRange(outOfBook);
        try
        {
            db.SaveChanges();
            tx.Commit();
        }
        catch (DbUpdateException)
        {
            // Ví dụ: trùng mã hóa đơn khi có thao tác ghi khác cùng lúc. Giao dịch được hủy, tồn kho không bị trừ.
            throw new BusinessException("Không lưu được hóa đơn do dữ liệu vừa thay đổi. Vui lòng bấm Thanh toán lại.");
        }
        return newOrder;
    }

    public void CancelOrder(int orderId, string reason, int adminId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessException("Hãy nhập lý do hủy hóa đơn.");

        using var db = _factory();
        using var tx = db.Database.BeginTransaction();

        var order = db.Orders.Include(o => o.Details).ThenInclude(d => d.Product).FirstOrDefault(o => o.Id == orderId)
                    ?? throw new BusinessException("Không tìm thấy hóa đơn.");
        if (order.IsCancelled)
            throw new BusinessException($"Hóa đơn {order.Code} đã hủy rồi.");

        order.IsCancelled = true;
        order.CancelledAt = DateTime.Now;
        order.CancelledByUserId = adminId;
        order.CancelReason = reason.Trim();

        foreach (var line in order.Details)
        {
            var product = line.Product!;
            // Dòng chưa có giá vốn thì không kéo bình quân về 0.
            if (line.UnitCost > 0)
                product.AverageCost = CostCalculator.AddToAverage(product.Stock, product.AverageCost, line.Quantity, line.UnitCost);
            StockLedger.Apply(db, product, product.Stock + line.Quantity, StockMovementType.SaleCancelled, adminId, order: order);
        }

        db.SaveChanges();
        tx.Commit();
    }

    public List<Order> GetHistory(DateTime? from = null, DateTime? to = null)
    {
        using var db = _factory();
        IQueryable<Order> query = db.Orders
            .AsNoTracking()
            .Include(o => o.Cashier)
            .Include(o => o.CancelledBy)
            .Include(o => o.Details).ThenInclude(d => d.Product);

        if (from.HasValue) query = query.Where(o => o.CreatedAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(o => o.CreatedAt < to.Value.Date.AddDays(1));

        return query.OrderByDescending(o => o.CreatedAt).ToList();
    }

    public Order? GetById(int id)
    {
        using var db = _factory();
        return db.Orders
            .AsNoTracking()
            .Include(o => o.Cashier)
            .Include(o => o.Details).ThenInclude(d => d.Product)
            .FirstOrDefault(o => o.Id == id);
    }

    public ProfitReport GetProfitReport(DateTime from, DateTime to)
    {
        if (from.Date > to.Date)
            throw new BusinessException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        // SQLite không cộng được kiểu decimal trong câu SQL, nên tổng hợp trong bộ nhớ.
        var details = GetHistory(from, to).Where(o => !o.IsCancelled).SelectMany(o => o.Details).ToList();

        DateTime start = from.Date, endExclusive = to.Date.AddDays(1);
        using var db = _factory();
        var resolved = db.StockDiscrepancies.AsNoTracking()
            .Where(d => d.Status == DiscrepancyStatus.Resolved && d.ResolvedAt >= start && d.ResolvedAt < endExclusive)
            .ToList();
        var collected = db.CompensationPayments.AsNoTracking()
            .Where(p => p.PaidAt >= start && p.PaidAt < endExclusive)
            .ToList();

        return new ProfitReport
        {
            LossExpense = resolved.Where(d => d.Resolution is DiscrepancyResolution.NormalLoss or DiscrepancyResolution.Compensation).Sum(d => d.Value),
            OtherIncome = resolved.Where(d => d.Resolution == DiscrepancyResolution.OtherIncome).Sum(d => d.Value),
            MiscountShortValue = resolved.Where(d => d.Resolution == DiscrepancyResolution.Miscount && d.IsShortage).Sum(d => d.Value),
            MiscountSurplusValue = resolved.Where(d => d.Resolution == DiscrepancyResolution.Miscount && !d.IsShortage).Sum(d => d.Value),
            CompensationCollected = collected.Sum(p => p.Amount),
            Revenue = details.Sum(d => d.LineTotal),
            CostOfGoods = details.Sum(d => d.LineCost),
            EstimatedLineCount = details.Count(d => d.CostIsEstimated),
            Products = details
                .GroupBy(d => d.ProductId)
                .Select(g => new ProductProfit
                {
                    Name = g.First().Product?.Name ?? $"Sản phẩm #{g.Key}",
                    Quantity = g.Sum(d => d.Quantity),
                    Revenue = g.Sum(d => d.LineTotal),
                    Cost = g.Sum(d => d.LineCost)
                })
                .OrderByDescending(p => p.Profit)
                .ToList()
        };
    }

    public RevenueReport GetRevenueReport(DateTime from, DateTime to)
    {
        DateTime start = from.Date, end = to.Date;
        if (start > end)
            throw new BusinessException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        var orders = GetHistory(start, end);
        var valid = orders.Where(o => !o.IsCancelled).ToList();

        // SQLite không cộng/sắp xếp được kiểu decimal trong câu SQL, nên tổng hợp trong bộ nhớ.
        decimal revenue = valid.Sum(o => o.Total);
        var details = valid.SelectMany(o => o.Details).ToList();

        var byDay = valid
            .GroupBy(o => o.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Revenue: g.Sum(o => o.Total)));

        // Khoảng ngắn: liệt kê đủ từng ngày (kể cả ngày không bán được); khoảng dài: chỉ những ngày có bán.
        IEnumerable<DateTime> days = (end - start).TotalDays <= 62
            ? Enumerable.Range(0, (int)(end - start).TotalDays + 1).Select(i => end.AddDays(-i))
            : byDay.Keys.OrderByDescending(d => d);

        var daily = days.Select(d => byDay.TryGetValue(d, out var v)
                ? new DailyRevenue { Date = d, OrderCount = v.Count, Revenue = v.Revenue }
                : new DailyRevenue { Date = d })
            .ToList();

        return new RevenueReport
        {
            From = start,
            To = end,
            Orders = orders,
            TotalRevenue = revenue,
            OrderCount = valid.Count,
            AverageOrderValue = valid.Count == 0 ? 0 : Math.Round(revenue / valid.Count, 0),
            ItemsSold = details.Sum(d => d.Quantity),
            Daily = daily
        };
    }
}
