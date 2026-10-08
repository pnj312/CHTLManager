using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CHTLManager.BLL.Services;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Views
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly UserService _userService = new UserService();

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Text = string.Empty;

            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
            {
                ErrorTextBlock.Text = "Vui lòng nhập tên đăng nhập và mật khẩu.";
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                ErrorTextBlock.Text = "Vui lòng nhập tên đăng nhập.";
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ErrorTextBlock.Text = "Vui lòng nhập mật khẩu.";
                return;
            }

            User? user;
            try
            {
                user = _userService.Login(username, password);
            }
            catch (Exception)
            {
                ErrorTextBlock.Text = "Không thể kết nối đến cơ sở dữ liệu. Vui lòng thử lại.";
                return;
            }

            if (user is null)
            {
                ErrorTextBlock.Text = "Tên đăng nhập hoặc mật khẩu không đúng.";
                return;
            }

            MainWindow mainWindow = new MainWindow(user);
            mainWindow.Show();
            Close();
        }
    }
}
