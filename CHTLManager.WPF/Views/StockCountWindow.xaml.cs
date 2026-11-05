using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Views;

/// <summary>Bước đếm của kiểm kê: tồn kho đúng ngay; phần chênh lệch để Admin xử lý ở tab Chênh lệch kho.</summary>
public partial class StockCountWindow : Window
{
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
        txtConclusion.Text = string.Empty;
        if (!TryReadCounted(out int counted)) return;

        int diff = counted - _product.Stock;
        txtConclusion.Text = diff switch
        {
            0 => "Khớp với phần mềm, không có chênh lệch.",
            < 0 => $"Thực tế ít hơn sổ {-diff} cái. Admin sẽ xử lý: mất, hỏng, hết hạn, bồi thường hay đếm sai.",
            _ => $"Thực tế nhiều hơn sổ {diff} cái. Admin sẽ xử lý: lô hàng chưa ghi hay đếm sai."
        };
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

        try
        {
            var discrepancy = AppServices.StockCounts.Count(_product.Id, counted, AppServices.CurrentUser!.Id);
            string message = $"Đã kiểm kê '{_product.Name}': tồn kho hiện là {counted}.";
            if (discrepancy != null) message += "\nPhần chênh lệch đang chờ Admin xử lý.";
            MessageBox.Show(message, "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            AppServices.NotifyChanged();
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }
}
