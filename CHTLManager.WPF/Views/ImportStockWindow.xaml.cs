using System.Windows;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Views;

/// <summary>Nhập một lô hàng theo hóa đơn của nhà cung cấp.</summary>
public partial class ImportStockWindow : Window
{
    private readonly Product _product;

    public ImportStockWindow(Product product)
    {
        InitializeComponent();
        _product = product;
        txtProduct.Text = product.Name;
        txtCurrent.Text = $"Tồn kho hiện tại: {product.StockDisplay}";
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => txtQuantity.Focus();

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        txtError.Text = string.Empty;

        if (!int.TryParse(txtQuantity.Text.Trim(), out int quantity) || quantity <= 0)
        {
            txtError.Text = "Số lượng nhập phải là số nguyên dương.";
            return;
        }

        try
        {
            var updated = AppServices.Products.ImportStock(_product.Id, quantity, txtSupplier.Text, txtInvoice.Text,
                AppServices.CurrentUser!.Id);
            MessageBox.Show($"Đã nhập {quantity} '{updated.Name}'. Tồn kho hiện là {updated.StockDisplay}.", "Thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }
}
