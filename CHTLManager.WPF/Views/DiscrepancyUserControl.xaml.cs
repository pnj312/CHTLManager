using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Tab Chênh lệch kho (chỉ Admin): xử lý phần lệch sau kiểm kê, người chịu trách nhiệm, thu bồi thường.</summary>
public partial class DiscrepancyUserControl : UserControl
{
    public DiscrepancyUserControl()
    {
        InitializeComponent();
        dpTo.SelectedDate = DateTime.Today;
        dpFrom.SelectedDate = DateTime.Today.AddDays(-30);
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => Reload();

    /// <summary>Mở từ nhắc việc: hiện mọi chênh lệch đang chờ, kể cả cũ.</summary>
    public void ShowPending()
    {
        dpFrom.SelectedDate = DateTime.Today.AddYears(-1);
        dpTo.SelectedDate = DateTime.Today;
        cboStatus.SelectedIndex = 1;
        Reload();
    }

    private void BtnView_Click(object sender, RoutedEventArgs e) => Reload();

    private void Reload()
    {
        if (dpFrom.SelectedDate is not DateTime from || dpTo.SelectedDate is not DateTime to)
        {
            ShowWarning("Hãy chọn đầy đủ ngày bắt đầu và ngày kết thúc (dd/MM/yyyy).");
            return;
        }

        DiscrepancyStatus? status = cboStatus.SelectedIndex switch
        {
            1 => DiscrepancyStatus.Pending,
            2 => DiscrepancyStatus.Resolved,
            _ => null
        };

        try
        {
            var all = AppServices.StockCounts.GetDiscrepancies(from, to, null);
            txtPending.Text = all.Count(d => d.Status == DiscrepancyStatus.Pending).ToString();
            txtLoss.Text = MoneyParser.Format(all.Where(d => d.Resolution is DiscrepancyResolution.NormalLoss or DiscrepancyResolution.Compensation).Sum(d => d.Value));
            txtCompensation.Text = MoneyParser.Format(all.Sum(d => d.CompensationRemaining));
            txtIncome.Text = MoneyParser.Format(all.Where(d => d.Resolution == DiscrepancyResolution.OtherIncome).Sum(d => d.Value));
            txtMiscount.Text = MoneyParser.Format(all.Where(d => d.Resolution == DiscrepancyResolution.Miscount).Sum(d => d.Value));

            var shown = status == null ? all : all.Where(d => d.Status == status).ToList();
            dgDiscrepancies.ItemsSource = shown;
            if (shown.Count > 0) dgDiscrepancies.SelectedIndex = 0;
            UpdateButtons();
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex)
        {
            MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DgDiscrepancies_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        var d = dgDiscrepancies.SelectedItem as StockDiscrepancy;
        btnResolve.IsEnabled = d?.Status == DiscrepancyStatus.Pending;
        btnCollect.IsEnabled = d is { CompensationRemaining: > 0 };
        btnPayments.IsEnabled = d is { Payments.Count: > 0 };
    }

    private void BtnResolve_Click(object sender, RoutedEventArgs e)
    {
        if (dgDiscrepancies.SelectedItem is not StockDiscrepancy d) return;
        if (new DiscrepancyResolveWindow(d) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void BtnCollect_Click(object sender, RoutedEventArgs e)
    {
        if (dgDiscrepancies.SelectedItem is not StockDiscrepancy d) return;

        string? text = ReasonWindow.Ask(Window.GetWindow(this), "Thu bồi thường",
            $"{d.ResponsibleDisplay} còn phải bồi thường {MoneyParser.Format(d.CompensationRemaining)}. Số tiền thu lần này (đ):");
        if (text == null) return;

        if (!MoneyParser.TryParse(text, out decimal amount))
        {
            ShowWarning("Số tiền phải là số nguyên, ví dụ 50000 hoặc 50.000.");
            return;
        }

        try
        {
            AppServices.StockCounts.CollectCompensation(d.Id, amount, null, AppServices.CurrentUser!.Id);
            Reload();
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex)
        {
            MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPayments_Click(object sender, RoutedEventArgs e)
    {
        if (dgDiscrepancies.SelectedItem is not StockDiscrepancy d) return;

        new CompensationPaymentsWindow(d) { Owner = Window.GetWindow(this) }.ShowDialog();
        Reload();
    }

    private static void ShowWarning(string message)
        => MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
}
