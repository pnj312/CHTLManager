using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

public interface IUserService
{
    /// <summary>Đăng nhập. Trả về User nếu đúng tài khoản/mật khẩu, ngược lại trả về null.</summary>
    User? Login(string username, string password);

    List<User> GetAll();

    User Create(string username, string password, string fullName, UserRole role);
}
