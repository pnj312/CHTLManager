using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace CHTLManager.WPF.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var user = AppServices.CurrentUser;
        string role = AppServices.IsAdmin ? "Admin" : "Thu ngân";
        txtUserInfo.Text = $"{user?.FullName} ({role})";

        // Phân quyền: chỉ Admin được vào tab Quản lý Kho, Hóa đơn & Doanh thu, Tài khoản
        if (!AppServices.IsAdmin)
        {
            tabInventory.Visibility = Visibility.Collapsed;
            tabReports.Visibility = Visibility.Collapsed;
            tabUsers.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        => new ChangePasswordWindow { Owner = this }.ShowDialog();

    private void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged của DataGrid bên trong cũng nổi bọt lên đây, nên phải lọc theo Source.
        if (!IsLoaded || !ReferenceEquals(e.Source, tabMain)) return;

        if (tabMain.SelectedItem == tabSales) posView.Reload();
        else if (tabMain.SelectedItem == tabInventory) productView.Reload();
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Bạn có chắc muốn đăng xuất?", "Đăng xuất",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        // Khởi động lại ứng dụng để quay về màn hình đăng nhập.
        string? exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });

        AppServices.CurrentUser = null;
        Application.Current.Shutdown();
    }
}
