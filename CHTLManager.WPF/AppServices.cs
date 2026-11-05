using CHTLManager.BLL;
using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF;

/// <summary>Nơi tập trung các Service (BLL) và phiên đăng nhập hiện tại cho tầng giao diện.</summary>
public static class AppServices
{
    public static IUserService Users { get; } = new UserService();
    public static IProductService Products { get; } = new ProductService();
    public static IOrderService Orders { get; } = new OrderService();
    public static IPurchaseService Purchases { get; } = new PurchaseService();
    public static IStockCountService StockCounts { get; } = new StockCountService();

    /// <summary>Có việc vừa thay đổi số phiếu đang chờ; cửa sổ chính dùng để cập nhật nhắc việc ngay.</summary>
    public static event Action? Changed;

    public static void NotifyChanged() => Changed?.Invoke();

    /// <summary>Người dùng đang đăng nhập.</summary>
    public static User? CurrentUser { get; set; }

    public static bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
}
