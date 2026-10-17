namespace CHTLManager.BLL;

/// <summary>Lỗi nghiệp vụ (dữ liệu không hợp lệ, hết hàng, thiếu tiền...). Thông báo có thể hiển thị trực tiếp cho người dùng.</summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message)
    {
    }
}
