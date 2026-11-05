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

    /// <summary>Người dùng đang đăng nhập.</summary>
    public static User? CurrentUser { get; set; }

    public static bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
}
