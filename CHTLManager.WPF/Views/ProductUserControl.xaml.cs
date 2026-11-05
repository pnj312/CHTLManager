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

    public void Reload() => Reload(null);

    /// <summary>Tải lại danh sách; nếu có <paramref name="selectId"/> thì chọn lại sản phẩm đó để form hiển thị số liệu mới.</summary>
    private void Reload(int? selectId)
    {
        try
        {
            var items = AppServices.Products.Search(txtSearch.Text);
            if (chkNeedsCheck.IsChecked == true)
                items = items.Where(p => p.NeedsStockCheck).ToList();
            dgProducts.ItemsSource = items;

            if (selectId.HasValue)
            {
                var again = items.FirstOrDefault(p => p.Id == selectId.Value);
                if (again != null)
                {
                    dgProducts.SelectedItem = again;
                    dgProducts.ScrollIntoView(again);
                }
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => Reload();

    private void ChkNeedsCheck_Changed(object sender, RoutedEventArgs e) => Reload();

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
            txtStock.Text = p.StockDisplay;
            // Đang sửa sản phẩm có sẵn: tồn kho chỉ thay đổi qua nhập kho hoặc kiểm kê.
            txtStock.IsEnabled = false;
        }
        else
        {
            ClearForm();
        }
    }

    // ---------------- Thêm / Sửa / Xóa ----------------

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        dgProducts.SelectedItem = null;
        ClearForm();
        txtBarcode.Focus();
    }

    private void ClearForm()
    {
        txtBarcode.Clear();
        txtName.Clear();
        txtCategory.Clear();
        txtPrice.Clear();
        txtStock.Clear();
        txtStock.IsEnabled = true;
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var product = ReadForm();
        if (product == null) return;

        try
        {
            var saved = AppServices.Products.Add(product, AppServices.CurrentUser!.Id);
            string message = saved.Stock == product.Stock
                ? "Đã thêm sản phẩm."
                : $"Mã vạch này thuộc sản phẩm đã ngừng kinh doanh nên được kinh doanh lại. Tồn kho giữ nguyên là {saved.StockDisplay}; muốn tăng tồn kho hãy dùng Nhập kho.";
            MessageBox.Show(message, "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
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
            Reload(selected.Id);
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
                MessageBox.Show("Sản phẩm đã có hóa đơn hoặc nhật ký kho nên được chuyển sang trạng thái ngừng kinh doanh (lịch sử được giữ nguyên).",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            Reload();
            BtnNew_Click(sender, e);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    // ---------------- Nhập kho ----------------

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        if (dgProducts.SelectedItem is not Product selected)
        {
            ShowWarning("Hãy chọn sản phẩm cần nhập kho.");
            return;
        }

        int id = selected.Id;
        Product? product = selected;

        // Hàng bán ngoài sổ thuộc lô trước: phải chốt lô đó bằng kiểm kê, rồi mới nhập lô mới.
        if (selected.NeedsStockCheck)
        {
            MessageBox.Show(
                $"'{selected.Name}' đã bán {-selected.Stock} cái ngoài sổ từ lô trước.\n\n" +
                "Hãy kiểm kê hàng đang có trước khi nhập lô mới, để hai lô không bị lẫn. " +
                "Lưu ý: đếm trước khi xếp hàng mới lên kệ.",
                "Cần kiểm kê trước", MessageBoxButton.OK, MessageBoxImage.Information);

            if (new StockCountWindow(selected) { Owner = Window.GetWindow(this) }.ShowDialog() != true) return;
            Reload(id);

            try { product = AppServices.Products.GetById(id); }
            catch (Exception ex) { ShowError(ex); return; }
            if (product == null) return;
        }

        if (new ImportStockWindow(product) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            Reload(id);
    }

    // ---------------- Kiểm kê ----------------

    private void BtnStockCount_Click(object sender, RoutedEventArgs e)
    {
        if (dgProducts.SelectedItem is not Product selected)
        {
            ShowWarning("Hãy chọn sản phẩm cần kiểm kê.");
            return;
        }

        var window = new StockCountWindow(selected) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true) Reload(selected.Id);
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

        if (!MoneyParser.TryParse(txtPrice.Text, out decimal price) || price <= 0)
        {
            ShowWarning("Giá bán phải là số nguyên dương (ví dụ 15000 hoặc 15.000).");
            return null;
        }

        // Ô tồn kho chỉ nhập khi thêm mới; khi sửa (ô bị khóa) nó chỉ để xem và Update không dùng đến.
        int stock = 0;
        if (txtStock.IsEnabled && (!int.TryParse(txtStock.Text.Trim(), out stock) || stock < 0))
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
