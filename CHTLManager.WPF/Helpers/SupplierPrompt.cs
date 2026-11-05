using System.Windows;

namespace CHTLManager.WPF.Helpers;

public static class SupplierPrompt
{
    /// <summary>
    /// Tên gõ vào gần giống nhà cung cấp đã có thì hỏi lại, vì tạo trùng sẽ làm công nợ bị tách đôi.
    /// Trả về tên sẽ dùng; null nghĩa là người dùng muốn quay lại sửa.
    /// </summary>
    public static string? Resolve(Window owner, string typed)
    {
        var similar = AppServices.Purchases.FindSimilarSupplier(typed);
        if (similar == null) return typed;

        var answer = MessageBox.Show(owner,
            $"Đã có nhà cung cấp '{similar.Name}', gần giống tên bạn gõ '{typed.Trim()}'.\n\n"
            + $"Có: dùng '{similar.Name}'.\nKhông: tạo nhà cung cấp mới '{typed.Trim()}', công nợ tính riêng.\nHủy: quay lại sửa.",
            "Nhà cung cấp gần giống", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);

        return answer switch
        {
            MessageBoxResult.Yes => similar.Name,
            MessageBoxResult.No => typed,
            _ => null
        };
    }
}
