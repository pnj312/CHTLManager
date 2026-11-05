using System.Windows;

namespace CHTLManager.WPF.Views;

/// <summary>Hỏi một lý do bắt buộc trước một thao tác không hoàn tác được.</summary>
public partial class ReasonWindow : Window
{
    private ReasonWindow(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        txtPrompt.Text = prompt;
        Loaded += (_, _) => txtReason.Focus();
    }

    /// <summary>Trả về lý do đã nhập, hoặc null nếu người dùng hủy bỏ.</summary>
    public static string? Ask(Window? owner, string title, string prompt)
    {
        var window = new ReasonWindow(title, prompt) { Owner = owner };
        return window.ShowDialog() == true ? window.txtReason.Text.Trim() : null;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtReason.Text))
        {
            txtError.Text = "Hãy nhập lý do.";
            return;
        }
        DialogResult = true;
    }
}
