using CHTLManager.BLL.Interfaces;
using CHTLManager.BLL.Reports;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL;

/// <summary>Tạo hóa đơn bán hàng, tính tổng tiền/tiền thừa và lưu lịch sử bán.</summary>
public class OrderService : IOrderService
{
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
                throw new BusinessException($"'{product.Name}' chỉ còn {product.Stock} trong kho.");

            product.Stock -= quantity;
            lines.Add(new OrderDetail
            {
                ProductId = product.Id,
                Product = product,
                Quantity = quantity,
                UnitPrice = product.Price // luôn lấy giá từ CSDL, không tin giá do giao diện gửi lên
            });
        }

        decimal total = CalculateTotal(lines);
        if (order.CustomerPaid < total)
            throw new BusinessException($"Tiền khách đưa chưa đủ (cần {total:N0} đ).");

        DateTime now = DateTime.Now;
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

    public List<Order> GetHistory(DateTime? from = null, DateTime? to = null)
    {
        using var db = _factory();
        IQueryable<Order> query = db.Orders
            .AsNoTracking()
            .Include(o => o.Cashier)
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

    public RevenueReport GetRevenueReport(DateTime from, DateTime to)
    {
        DateTime start = from.Date, end = to.Date;
        if (start > end)
            throw new BusinessException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        var orders = GetHistory(start, end);

        // SQLite không cộng/sắp xếp được kiểu decimal trong câu SQL, nên tổng hợp trong bộ nhớ.
        decimal revenue = orders.Sum(o => o.Total);
        var details = orders.SelectMany(o => o.Details).ToList();

        var byDay = orders
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

        var top = details
            .GroupBy(d => d.ProductId)
            .Select(g => new
            {
                Name = g.First().Product?.Name ?? $"Sản phẩm #{g.Key}",
                Quantity = g.Sum(d => d.Quantity),
                Revenue = g.Sum(d => d.LineTotal)
            })
            .OrderByDescending(x => x.Revenue)
            .ThenByDescending(x => x.Quantity)
            .Take(5)
            .Select((x, i) => new TopProduct { Rank = i + 1, Name = x.Name, Quantity = x.Quantity, Revenue = x.Revenue })
            .ToList();

        return new RevenueReport
        {
            From = start,
            To = end,
            Orders = orders,
            TotalRevenue = revenue,
            OrderCount = orders.Count,
            AverageOrderValue = orders.Count == 0 ? 0 : Math.Round(revenue / orders.Count, 0),
            ItemsSold = details.Sum(d => d.Quantity),
            Daily = daily,
            TopProducts = top
        };
    }
}
