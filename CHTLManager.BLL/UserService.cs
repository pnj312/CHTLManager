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
            .FirstOrDefault(u => u.Username.ToLower() == name && u.IsActive);

        if (user == null) return null;
        return PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
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
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            throw new BusinessException("Mật khẩu phải có ít nhất 6 ký tự.");
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
}
