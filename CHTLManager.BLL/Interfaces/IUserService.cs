using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

public interface IUserService
{
    /// <summary>
    /// Đăng nhập. Trả về User nếu đúng tài khoản/mật khẩu, ngược lại trả về null.
    /// Ném BusinessException nếu đúng mật khẩu nhưng tài khoản đã bị khóa.
    /// </summary>
    User? Login(string username, string password);

    List<User> GetAll();

    User Create(string username, string password, string fullName, UserRole role);

    /// <summary>Khóa / mở khóa. Không cho tự khóa mình và không cho khóa Admin cuối cùng đang hoạt động.</summary>
    void SetActive(int userId, bool active, int currentUserId);

    /// <summary>Admin đặt lại mật khẩu cho tài khoản khác (không cần mật khẩu cũ).</summary>
    void ResetPassword(int userId, string newPassword);

    /// <summary>Người dùng tự đổi mật khẩu, phải nhập đúng mật khẩu hiện tại.</summary>
    void ChangePassword(int userId, string oldPassword, string newPassword);
}
