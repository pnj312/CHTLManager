using CHTLManager.BLL.Interfaces;
using CHTLManager.BLL.Security;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL;

/// <summary>Đăng nhập và xác thực vai trò (Admin / Thu ngân).</summary>
public class UserService : IUserService
{
    private readonly Func<AppDbContext> _factory;

    public UserService() : this(() => new AppDbContext())
    {
    }

    public UserService(Func<AppDbContext> factory)
    {
        _factory = factory;
    }

    public User? Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        string name = username.Trim().ToLower();

        using var db = _factory();
        var user = db.Users
            .AsNoTracking()
            .FirstOrDefault(u => u.Username.ToLower() == name);

        if (user == null || !PasswordHasher.Verify(password, user.PasswordHash)) return null;
        // Chỉ báo "bị khóa" khi đã đúng mật khẩu, để không lộ thông tin tài khoản cho người đoán mò.
        if (!user.IsActive) throw new BusinessException("Tài khoản đã bị khóa. Vui lòng liên hệ Admin.");
        return user;
    }

    public List<User> GetAll()
    {
        using var db = _factory();
        return db.Users.AsNoTracking().OrderBy(u => u.Username).ToList();
    }

    public User Create(string username, string password, string fullName, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new BusinessException("Tên đăng nhập không được để trống.");
        ValidatePassword(password);
        if (string.IsNullOrWhiteSpace(fullName))
            throw new BusinessException("Họ tên không được để trống.");

        string name = username.Trim();

        using var db = _factory();
        if (db.Users.Any(u => u.Username.ToLower() == name.ToLower()))
            throw new BusinessException($"Tên đăng nhập '{name}' đã tồn tại.");

        var user = new User
        {
            Username = name,
            PasswordHash = PasswordHasher.Hash(password),
            FullName = fullName.Trim(),
            Role = role,
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public void SetActive(int userId, bool active, int currentUserId)
    {
        using var db = _factory();
        var user = db.Users.FirstOrDefault(u => u.Id == userId)
                   ?? throw new BusinessException("Không tìm thấy tài khoản.");

        if (!active)
        {
            if (userId == currentUserId)
                throw new BusinessException("Không thể tự khóa tài khoản đang đăng nhập.");
            if (user.Role == UserRole.Admin && user.IsActive &&
                db.Users.Count(u => u.Role == UserRole.Admin && u.IsActive) <= 1)
                throw new BusinessException("Không thể khóa Admin cuối cùng đang hoạt động.");
        }

        user.IsActive = active;
        db.SaveChanges();
    }

    public void ResetPassword(int userId, string newPassword)
    {
        ValidatePassword(newPassword);

        using var db = _factory();
        var user = db.Users.FirstOrDefault(u => u.Id == userId)
                   ?? throw new BusinessException("Không tìm thấy tài khoản.");
        user.PasswordHash = PasswordHasher.Hash(newPassword);
        db.SaveChanges();
    }

    public void ChangePassword(int userId, string oldPassword, string newPassword)
    {
        using var db = _factory();
        var user = db.Users.FirstOrDefault(u => u.Id == userId)
                   ?? throw new BusinessException("Không tìm thấy tài khoản.");

        if (!PasswordHasher.Verify(oldPassword ?? string.Empty, user.PasswordHash))
            throw new BusinessException("Mật khẩu hiện tại không đúng.");
        ValidatePassword(newPassword);

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        db.SaveChanges();
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            throw new BusinessException("Mật khẩu phải có ít nhất 6 ký tự.");
    }
}
