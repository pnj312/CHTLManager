using System.Windows;
using CHTLManager.BLL;

namespace CHTLManager.WPF.Views;

/// <summary>Người dùng đang đăng nhập tự đổi mật khẩu của mình.</summary>
public partial class ChangePasswordWindow : Window
{
    public ChangePasswordWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => txtOld.Focus();

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        txtError.Text = string.Empty;

        if (txtNew.Password != txtConfirm.Password)
        {
            txtError.Text = "Mật khẩu nhập lại không khớp.";
            return;
        }

        try
        {
            AppServices.Users.ChangePassword(AppServices.CurrentUser!.Id, txtOld.Password, txtNew.Password);
            MessageBox.Show("Đã đổi mật khẩu. Lần đăng nhập sau hãy dùng mật khẩu mới.", "Thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }
}
