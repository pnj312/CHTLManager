using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Views;

/// <summary>Kiểm kê một sản phẩm: so số trên phần mềm với số đếm thực tế, lệch thì bắt buộc chọn lý do.</summary>
public partial class StockCountWindow : Window
{
    private static readonly StockReason[] LessReasons =
        { StockReason.Lost, StockReason.Damaged, StockReason.Expired, StockReason.MiscountBefore };
    private static readonly StockReason[] MoreReasons =
        { StockReason.UnrecordedImport, StockReason.MiscountBefore };

    private readonly Product _product;

    public StockCountWindow(Product product)
    {
        InitializeComponent();
        _product = product;
        txtProduct.Text = product.Name;
        txtBook.Text = product.StockDisplay;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => txtCounted.Focus();

    private void TxtCounted_TextChanged(object sender, TextChangedEventArgs e)
    {
        txtError.Text = string.Empty;
        cboReason.Items.Clear();
        cboReason.IsEnabled = false;
        txtConclusion.Text = string.Empty;

        if (!TryReadCounted(out int counted)) return;

        int diff = counted - _product.Stock;
        if (diff == 0)
        {
            txtConclusion.Text = "Khớp với phần mềm, không có chênh lệch.";
            return;
        }

        txtConclusion.Text = diff < 0
            ? $"Thực tế ít hơn sổ {-diff} cái: thất thoát. Hãy chọn lý do."
            : $"Thực tế nhiều hơn sổ {diff} cái: hàng nhập chưa ghi hoặc đếm sai trước đó.";

        foreach (var reason in diff < 0 ? LessReasons : MoreReasons)
            cboReason.Items.Add(new ComboBoxItem { Content = StockMovement.ReasonName(reason), Tag = reason });
        cboReason.IsEnabled = true;
    }

    private bool TryReadCounted(out int counted)
        => int.TryParse(txtCounted.Text.Trim(), out counted) && counted >= 0;

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        txtError.Text = string.Empty;

        if (!TryReadCounted(out int counted))
        {
            txtError.Text = "Số đếm được phải là số nguyên không âm.";
            return;
        }

        var reason = (cboReason.SelectedItem as ComboBoxItem)?.Tag as StockReason? ?? StockReason.None;

        try
        {
            var updated = AppServices.Products.SetCountedStock(_product.Id, counted, reason, AppServices.CurrentUser!.Id);
            MessageBox.Show($"Đã kiểm kê '{updated.Name}': tồn kho hiện là {updated.StockDisplay}.", "Thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }
}
