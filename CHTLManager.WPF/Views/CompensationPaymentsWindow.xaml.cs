using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Xem các lần thu bồi thường của một phiếu chênh lệch và hủy lần ghi nhầm.</summary>
public partial class CompensationPaymentsWindow : Window
{
    private readonly StockDiscrepancy _discrepancy;

    public CompensationPaymentsWindow(StockDiscrepancy discrepancy)
    {
        InitializeComponent();
        _discrepancy = discrepancy;
        Reload();
    }

    private void Reload()
    {
        var payments = AppServices.StockCounts.GetCompensationPayments(_discrepancy.Id);
        decimal collected = payments.Where(p => !p.IsCancelled).Sum(p => p.Amount);
        decimal amount = _discrepancy.CompensationAmount ?? 0;

        txtHeader.Text = $"{_discrepancy.ResponsibleDisplay} – {_discrepancy.Product?.Name}: phải bồi thường {MoneyParser.Format(amount)}, "
                         + $"đã thu {MoneyParser.Format(collected)}, còn {MoneyParser.Format(amount - collected)}.";
        dgPayments.ItemsSource = payments;
        if (payments.Count > 0) dgPayments.SelectedIndex = 0;
        UpdateButton();
    }

    private void DgPayments_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButton();

    private void UpdateButton()
        => btnCancelPayment.IsEnabled = dgPayments.SelectedItem is CompensationPayment { IsCancelled: false };

    private void BtnCancelPayment_Click(object sender, RoutedEventArgs e)
    {
        if (dgPayments.SelectedItem is not CompensationPayment payment) return;

        string? reason = ReasonWindow.Ask(this, "Hủy lần thu",
            $"Hủy lần thu {MoneyParser.Format(payment.Amount)}? Số còn phải thu sẽ tăng lại; lần thu vẫn được giữ trong danh sách. Lý do hủy:");
        if (reason == null) return;

        try
        {
            AppServices.StockCounts.CancelCompensationPayment(payment.Id, reason, AppServices.CurrentUser!.Id);
            Reload();
        }
        catch (BusinessException ex)
        {
            MessageBox.Show(ex.Message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
