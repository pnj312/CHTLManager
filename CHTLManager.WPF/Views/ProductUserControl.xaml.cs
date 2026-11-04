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
            txtStock.Text = p.Stock.ToString();
            // Đang sửa sản phẩm có sẵn: tồn kho chỉ thay đổi qua "Nhập kho nhanh".
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
        txtImportQty.Clear();
        txtCountedQty.Clear();
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
            Reload(selected.Id);
            txtImportQty.Clear();
            MessageBox.Show($"Tồn kho của '{updated.Name}' hiện là {updated.Stock}.", "Thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    // ---------------- Kiểm kê ----------------

    /// <summary>Đặt tồn kho bằng số đếm thực tế (xử lý kho âm do bán vượt tồn, hoặc hàng mất/hỏng).</summary>
    private void BtnStockCount_Click(object sender, RoutedEventArgs e)
    {
        if (dgProducts.SelectedItem is not Product selected)
        {
            ShowWarning("Hãy chọn sản phẩm cần kiểm kê.");
            return;
        }

        if (!int.TryParse(txtCountedQty.Text.Trim(), out int counted) || counted < 0)
        {
            ShowWarning("Số lượng kiểm kê phải là số nguyên không âm.");
            return;
        }

        int diff = counted - selected.Stock;
        string diffText = diff > 0 ? $"+{diff} (phần mềm đang ghi thiếu)"
                        : diff < 0 ? $"{diff} (hàng thực tế ít hơn: mất, hỏng hoặc đếm sai)"
                        : "0 (khớp)";
        if (MessageBox.Show(
                $"Sản phẩm: {selected.Name}\nTồn trên phần mềm: {selected.Stock}\nĐếm thực tế: {counted}\nChênh lệch: {diffText}\n\nCập nhật tồn kho theo số đếm thực tế?",
                "Xác nhận kiểm kê", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            var updated = AppServices.Products.SetCountedStock(selected.Id, counted);
            Reload(selected.Id);
            txtCountedQty.Clear();
            MessageBox.Show($"Đã kiểm kê '{updated.Name}': tồn kho hiện là {updated.Stock}.", "Thành công",
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

        if (!MoneyParser.TryParse(txtPrice.Text, out decimal price) || price <= 0)
        {
            ShowWarning("Giá bán phải là số nguyên dương (ví dụ 15000 hoặc 15.000).");
            return null;
        }

        // Ô tồn kho chỉ nhập khi thêm mới; khi sửa (ô bị khóa) tồn kho có thể âm do bán vượt tồn và không được gửi đi.
        if (!int.TryParse(txtStock.Text.Trim(), out int stock) || (txtStock.IsEnabled && stock < 0))
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
