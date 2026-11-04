using System.Windows;
using CHTLManager.BLL;

namespace CHTLManager.WPF.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtUsername.Focus();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        txtError.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(txtUsername.Text) || txtPassword.Password.Length == 0)
        {
            txtError.Text = "Vui lòng nhập tên đăng nhập và mật khẩu.";
            return;
        }

        try
        {
            var user = AppServices.Users.Login(txtUsername.Text, txtPassword.Password);
            if (user == null)
            {
                txtError.Text = "Sai tên đăng nhập hoặc mật khẩu.";
                txtPassword.Clear();
                txtPassword.Focus();
                return;
            }

            AppServices.CurrentUser = user;
            DialogResult = true;
        }
        catch (BusinessException ex)
        {
            txtError.Text = ex.Message;
            txtPassword.Clear();
        }
        catch (Exception ex)
        {
            txtError.Text = "Lỗi hệ thống: " + ex.Message;
        }
    }
}
