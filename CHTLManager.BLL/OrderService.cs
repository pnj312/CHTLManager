using CHTLManager.BLL.Interfaces;
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

    public Order CreateOrder(Order order, List<OrderDetail> details)
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
            if (product.Stock < quantity)
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
}
