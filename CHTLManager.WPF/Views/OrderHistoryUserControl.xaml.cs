using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Màn hình lịch sử hóa đơn và thống kê doanh thu (chỉ Admin).</summary>
public partial class OrderHistoryUserControl : UserControl
{
    public OrderHistoryUserControl()
    {
        InitializeComponent();

        // Mặc định xem 7 ngày gần nhất.
        dpTo.SelectedDate = DateTime.Today;
        dpFrom.SelectedDate = DateTime.Today.AddDays(-6);
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => Reload();

    /// <summary>Tải lại báo cáo theo khoảng ngày đang chọn (gọi khi mở tab).</summary>
    public void Reload()
    {
        if (dpFrom.SelectedDate is not DateTime from || dpTo.SelectedDate is not DateTime to)
        {
            ShowWarning("Hãy chọn đầy đủ ngày bắt đầu và ngày kết thúc (dd/MM/yyyy).");
            return;
        }

        try
        {
            var report = AppServices.Orders.GetRevenueReport(from, to);

            txtRevenue.Text = MoneyParser.Format(report.TotalRevenue);
            txtOrderCount.Text = report.OrderCount.ToString("N0");
            txtAverage.Text = MoneyParser.Format(report.AverageOrderValue);
            txtItemsSold.Text = report.ItemsSold.ToString("N0");

            dgOrders.ItemsSource = report.Orders;
            dgDaily.ItemsSource = report.Daily;
            dgTop.ItemsSource = report.TopProducts;

            if (report.Orders.Count > 0) dgOrders.SelectedIndex = 0;
            else ShowDetail(null);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex)
        {
            MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowDetail(dgOrders.SelectedItem as Order);
        e.Handled = true; // không để sự kiện nổi lên TabControl của cửa sổ chính
    }

    private void ShowDetail(Order? order)
    {
        if (order == null)
        {
            txtDetailTitle.Text = "CHI TIẾT HÓA ĐƠN";
            dgDetails.ItemsSource = null;
            txtDetailTotal.Text = txtDetailPaid.Text = txtDetailChange.Text = string.Empty;
            return;
        }

        txtDetailTitle.Text = $"CHI TIẾT {order.Code}";
        dgDetails.ItemsSource = order.Details;
        txtDetailTotal.Text = MoneyParser.Format(order.Total);
        txtDetailPaid.Text = MoneyParser.Format(order.CustomerPaid);
        txtDetailChange.Text = MoneyParser.Format(order.Change);
    }

    // ---------------- Chọn nhanh khoảng ngày ----------------

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
