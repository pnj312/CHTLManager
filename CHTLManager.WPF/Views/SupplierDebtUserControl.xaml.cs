using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Tab Công nợ NCC (chỉ Admin): nợ từng nhà cung cấp, trả tiền, lịch sử.</summary>
public partial class SupplierDebtUserControl : UserControl
{
    public SupplierDebtUserControl()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => Reload(null);

    private void Reload(int? selectSupplierId)
    {
        try
        {
            var balances = AppServices.Purchases.GetSupplierBalances();
            dgSuppliers.ItemsSource = balances;
            txtTotalDebt.Text = MoneyParser.Format(balances.Sum(b => b.Balance));

            var selected = balances.FirstOrDefault(b => b.Supplier.Id == selectSupplierId) ?? balances.FirstOrDefault();
            dgSuppliers.SelectedItem = selected;
            if (selected == null) ShowHistory(null);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void DgSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ShowHistory(dgSuppliers.SelectedItem as SupplierBalance);

    private void ShowHistory(SupplierBalance? balance)
    {
        btnPay.IsEnabled = balance is { Balance: > 0 };
        btnCancelPayment.IsEnabled = false;

        if (balance == null)
        {
            txtHistory.Text = "LỊCH SỬ CÔNG NỢ";
            dgHistory.ItemsSource = null;
            return;
        }

        txtHistory.Text = $"LỊCH SỬ CÔNG NỢ: {balance.Supplier.Name}";
        try { dgHistory.ItemsSource = AppServices.Purchases.GetSupplierHistory(balance.Supplier.Id); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void DgHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => btnCancelPayment.IsEnabled = dgHistory.SelectedItem is SupplierLedgerEntry { PaymentId: not null, IsCancelled: false };

    private void BtnAddSupplier_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var supplier = AppServices.Purchases.SaveSupplier(new Supplier { Name = txtNewSupplier.Text });
            txtNewSupplier.Clear();
            Reload(supplier.Id);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void BtnPay_Click(object sender, RoutedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is not SupplierBalance balance) return;
        if (new SupplierPaymentWindow(balance) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            Reload(balance.Supplier.Id);
    }

    private void BtnCancelPayment_Click(object sender, RoutedEventArgs e)
    {
        if (dgHistory.SelectedItem is not SupplierLedgerEntry { PaymentId: int paymentId } entry
            || dgSuppliers.SelectedItem is not SupplierBalance balance) return;

        string? reason = ReasonWindow.Ask(Window.GetWindow(this), "Hủy lần trả tiền",
            $"Hủy lần trả {MoneyParser.Format(entry.Decrease)}? Số nợ sẽ tăng lại đúng số này. Lý do hủy:");
        if (reason == null) return;

        try
        {
            AppServices.Purchases.CancelPayment(paymentId, reason, AppServices.CurrentUser!.Id);
            Reload(balance.Supplier.Id);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    private static void ShowWarning(string message)
        => MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static void ShowError(Exception ex)
        => MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
}
