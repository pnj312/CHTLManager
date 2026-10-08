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
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ProductTabItem.Visibility = Visibility.Collapsed;
            MainTabControl.SelectedItem = PosTabItem;
        }

        public MainWindow(User currentUser) : this()
        {
            CurrentUserTextBlock.Text = string.IsNullOrWhiteSpace(currentUser.FullName)
                ? currentUser.Username
                : currentUser.FullName;
            CurrentRoleTextBlock.Text = currentUser.Role;
            ProductTabItem.Visibility = string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
            PosUserControl.SetCurrentUser(currentUser);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirmation = MessageBox.Show(
                this,
                "Bạn có chắc muốn đăng xuất?",
                "Xác nhận đăng xuất",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
    }
}
