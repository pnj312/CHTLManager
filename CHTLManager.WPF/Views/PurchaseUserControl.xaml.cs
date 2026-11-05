using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Views;

/// <summary>Tab Nhập hàng (chỉ Admin): danh sách phiếu nhập, xác nhận và hủy phiếu.</summary>
public partial class PurchaseUserControl : UserControl
{
    public PurchaseUserControl()
    {
        InitializeComponent();
        dpTo.SelectedDate = DateTime.Today;
        dpFrom.SelectedDate = DateTime.Today.AddDays(-30);
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => Reload();

    /// <summary>Mở từ nhắc việc: hiện mọi phiếu đang chờ, kể cả phiếu cũ.</summary>
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

        ReceiptStatus? status = cboStatus.SelectedIndex switch
        {
            1 => ReceiptStatus.Pending,
            2 => ReceiptStatus.Confirmed,
            3 => ReceiptStatus.Cancelled,
            _ => null
        };

        try
        {
            var receipts = AppServices.Purchases.GetReceipts(from, to, status);
            dgReceipts.ItemsSource = receipts;
            if (receipts.Count > 0) dgReceipts.SelectedIndex = 0;
            else ShowDetail(null);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void DgReceipts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ShowDetail(dgReceipts.SelectedItem as PurchaseReceipt);

    private void ShowDetail(PurchaseReceipt? receipt)
    {
        dgLines.ItemsSource = receipt?.Lines;
        btnConfirm.IsEnabled = receipt?.Status == ReceiptStatus.Pending;
        btnCorrect.IsEnabled = receipt?.Status == ReceiptStatus.Confirmed;
        btnCancel.IsEnabled = receipt != null && receipt.Status != ReceiptStatus.Cancelled;

        if (receipt == null)
        {
            txtDetail.Text = "CHI TIẾT PHIẾU";
            return;
        }

        txtDetail.Text = receipt.Status == ReceiptStatus.Cancelled
            ? $"CHI TIẾT {receipt.Code} – Đã hủy: {receipt.CancelReason}"
            : $"CHI TIẾT {receipt.Code}";
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        if (new PurchaseReceiptWindow { Owner = Window.GetWindow(this) }.ShowDialog() == true) ReloadAfterChange();
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (dgReceipts.SelectedItem is not PurchaseReceipt receipt) return;
        if (new PurchaseConfirmWindow(receipt) { Owner = Window.GetWindow(this) }.ShowDialog() == true) ReloadAfterChange();
    }

    private void BtnCorrect_Click(object sender, RoutedEventArgs e)
    {
        if (dgReceipts.SelectedItem is not PurchaseReceipt receipt) return;
        if (new PurchaseConfirmWindow(receipt, correcting: true) { Owner = Window.GetWindow(this) }.ShowDialog() == true) ReloadAfterChange();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (dgReceipts.SelectedItem is not PurchaseReceipt receipt) return;

        string? reason = ReasonWindow.Ask(Window.GetWindow(this), "Hủy phiếu nhập",
            $"Hủy phiếu {receipt.Code}? Tồn kho sẽ bị trừ lại đúng số đã cộng. Chỉ hủy khi hàng thực sự không nhập; nếu chỉ sai giá, hãy dùng Sửa giá nhập, vì hủy rồi nhập lại sẽ làm sai tồn kho khi hàng đã bán. Lý do hủy:");
        if (reason == null) return;

        try
        {
            AppServices.Purchases.CancelReceipt(receipt.Id, reason, AppServices.CurrentUser!.Id);
            ReloadAfterChange();
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ReloadAfterChange()
    {
        Reload();
        AppServices.NotifyChanged();
    }

    private static void ShowWarning(string message)
        => MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static void ShowError(Exception ex)
        => MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
}
