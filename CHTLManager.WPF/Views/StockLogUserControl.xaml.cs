using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;

namespace CHTLManager.WPF.Views;

/// <summary>Nhật ký kho (chỉ Admin, chỉ xem): mọi lần tồn kho đổi ngoài bán hàng thường, thất thoát xem ở tab Chênh lệch kho.</summary>
public partial class StockLogUserControl : UserControl
{
    public StockLogUserControl()
    {
        InitializeComponent();
        dpTo.SelectedDate = DateTime.Today;
        dpFrom.SelectedDate = DateTime.Today.AddDays(-6);
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => Reload();

    private void Reload()
    {
        if (dpFrom.SelectedDate is not DateTime from || dpTo.SelectedDate is not DateTime to)
        {
            ShowWarning("Hãy chọn đầy đủ ngày bắt đầu và ngày kết thúc (dd/MM/yyyy).");
            return;
        }

        try
        {
            var report = AppServices.Products.GetStockLog(from, to);
            txtRows.Text = report.Movements.Count.ToString("N0");
            txtOutOfBook.Text = report.OutOfBookQuantity.ToString("N0");
            dgLog.ItemsSource = report.Movements;
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex)
        {
            MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnView_Click(object sender, RoutedEventArgs e) => Reload();

    private void BtnToday_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today, DateTime.Today);

    private void BtnWeek_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today.AddDays(-6), DateTime.Today);

    private void BtnMonth_Click(object sender, RoutedEventArgs e)
        => SetRange(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);

    private void SetRange(DateTime from, DateTime to)
    {
        dpFrom.SelectedDate = from;
        dpTo.SelectedDate = to;
        Reload();
    }

    private static void ShowWarning(string message)
        => MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
}
