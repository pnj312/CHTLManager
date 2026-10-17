using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Màn hình quản lý sản phẩm / kho (chỉ Admin).</summary>
public partial class ProductUserControl : UserControl
{
    public ProductUserControl()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => Reload();

    public void Reload()
    {
        try
        {
            dgProducts.ItemsSource = AppServices.Products.Search(txtSearch.Text);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => Reload();

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        txtSearch.Clear();
        Reload();
    }

    private void DgProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgProducts.SelectedItem is Product p)
        {
            txtBarcode.Text = p.Barcode;
            txtName.Text = p.Name;
            txtCategory.Text = p.Category;
            txtPrice.Text = p.Price.ToString("0");
            txtStock.Text = p.Stock.ToString();
        }
    }

    // ---------------- Thêm / Sửa / Xóa ----------------

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        dgProducts.SelectedItem = null;
        txtBarcode.Clear();
        txtName.Clear();
        txtCategory.Clear();
        txtPrice.Clear();
        txtStock.Clear();
        txtImportQty.Clear();
        txtBarcode.Focus();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var product = ReadForm();
        if (product == null) return;

        try
        {
            AppServices.Products.Add(product);
            MessageBox.Show("Đã thêm sản phẩm.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            Reload();
            BtnNew_Click(sender, e);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (dgProducts.SelectedItem is not Product selected)
        {
            ShowWarning("Hãy chọn sản phẩm cần sửa trong danh sách.");
            return;
        }

        var product = ReadForm();
        if (product == null) return;
        product.Id = selected.Id;

        try
        {
            AppServices.Products.Update(product);
            MessageBox.Show("Đã cập nhật sản phẩm.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            Reload();
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgProducts.SelectedItem is not Product selected)
        {
            ShowWarning("Hãy chọn sản phẩm cần xóa trong danh sách.");
            return;
        }

        if (MessageBox.Show($"Xóa sản phẩm '{selected.Name}'?", "Xác nhận",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            bool removed = AppServices.Products.Delete(selected.Id);
            if (!removed)
                MessageBox.Show("Sản phẩm đã có trong hóa đơn nên được chuyển sang trạng thái ngừng kinh doanh (lịch sử bán hàng được giữ nguyên).",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            Reload();
            BtnNew_Click(sender, e);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    // ---------------- Nhập kho nhanh ----------------

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        if (dgProducts.SelectedItem is not Product selected)
        {
            ShowWarning("Hãy chọn sản phẩm cần nhập kho.");
            return;
        }

        if (!int.TryParse(txtImportQty.Text.Trim(), out int qty) || qty <= 0)
        {
            ShowWarning("Số lượng nhập phải là số nguyên dương.");
            return;
        }

        try
        {
            var updated = AppServices.Products.UpdateStock(selected.Id, qty);
            txtImportQty.Clear();
            Reload();
            MessageBox.Show($"Tồn kho của '{updated.Name}' hiện là {updated.Stock}.", "Thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    // ---------------- Tiện ích ----------------

    /// <summary>Đọc và kiểm tra dữ liệu trên form; trả về null nếu không hợp lệ.</summary>
    private Product? ReadForm()
    {
        if (string.IsNullOrWhiteSpace(txtBarcode.Text) || string.IsNullOrWhiteSpace(txtName.Text))
        {
            ShowWarning("Mã vạch và tên sản phẩm không được để trống.");
            return null;
        }

        if (!MoneyParser.TryParse(txtPrice.Text, out decimal price))
        {
            ShowWarning("Giá bán không hợp lệ.");
            return null;
        }

        if (!int.TryParse(txtStock.Text.Trim(), out int stock) || stock < 0)
        {
            ShowWarning("Tồn kho phải là số nguyên không âm.");
            return null;
        }

        return new Product
        {
            Barcode = txtBarcode.Text.Trim(),
            Name = txtName.Text.Trim(),
            Category = txtCategory.Text.Trim(),
            Price = price,
            Stock = stock
        };
    }

    private static void ShowWarning(string message)
        => MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static void ShowError(Exception ex)
        => MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
}
