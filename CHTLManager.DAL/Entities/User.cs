using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CHTLManager.DAL.Entities;

/// <summary>Vai trò của người dùng trong hệ thống.</summary>
public enum UserRole
{
    Admin = 1,
    Cashier = 2
}

/// <summary>Tài khoản người dùng (Admin hoặc Thu ngân).</summary>
public class User
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    /// <summary>Mật khẩu đã băm (PBKDF2), không lưu mật khẩu gốc.</summary>
    [Required, MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Cashier;

    public bool IsActive { get; set; } = true;

    [NotMapped]
    public string RoleName => Role == UserRole.Admin ? "Admin" : "Thu ngân";

    [NotMapped]
    public string StatusName => IsActive ? "Đang hoạt động" : "Đã khóa";

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
