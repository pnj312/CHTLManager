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

        // Phân quyền: chỉ Admin được vào các tab quản lý
        if (!AppServices.IsAdmin)
        {
            tabInventory.Visibility = Visibility.Collapsed;
            tabPurchases.Visibility = Visibility.Collapsed;
            tabReports.Visibility = Visibility.Collapsed;
            tabStockLog.Visibility = Visibility.Collapsed;
            tabUsers.Visibility = Visibility.Collapsed;
        }

        purchaseView.ReceiptsChanged += RefreshReminder;
        RefreshReminder();
    }

    /// <summary>Nhắc Admin các việc đang chờ; cập nhật khi đăng nhập và khi chuyển tab.</summary>
    private void RefreshReminder()
    {
        if (!AppServices.IsAdmin) return;

        int pending = AppServices.Purchases.CountPendingReceipts();
        btnReminder.Content = $"{pending} phiếu chờ xác nhận";
        btnReminder.Visibility = pending > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnReminder_Click(object sender, RoutedEventArgs e)
    {
        tabMain.SelectedItem = tabPurchases;
        purchaseView.ShowPending();
    }

    private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        => new ChangePasswordWindow { Owner = this }.ShowDialog();

    private void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged của DataGrid bên trong cũng nổi bọt lên đây, nên phải lọc theo Source.
        if (!IsLoaded || !ReferenceEquals(e.Source, tabMain)) return;

        if (tabMain.SelectedItem == tabSales) posView.Reload();
        else if (tabMain.SelectedItem == tabInventory) productView.Reload();
        RefreshReminder();
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
