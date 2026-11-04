using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;
using CHTLManager.WPF.Models;

namespace CHTLManager.WPF.Views;

/// <summary>Màn hình bán hàng: quét mã vạch / chọn món, giỏ hàng, thanh toán.</summary>
public partial class POSUserControl : UserControl
{
    private readonly ObservableCollection<CartItem> _cart = new();

    public POSUserControl()
    {
        InitializeComponent();
        dgCart.ItemsSource = _cart;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Reload();
        txtBarcode.Focus();
    }

    /// <summary>Tải lại danh sách sản phẩm (gọi khi mở tab hoặc sau khi thanh toán).</summary>
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

    // ---------------- Thêm sản phẩm vào giỏ ----------------

    private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        string code = txtBarcode.Text.Trim();
        if (code.Length == 0) return;

        try
        {
            var product = AppServices.Products.GetByBarcode(code);
            if (product == null)
                MessageBox.Show($"Không tìm thấy sản phẩm có mã vạch '{code}'.", "Không tìm thấy",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                AddToCart(product);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }

        txtBarcode.Clear();
        txtBarcode.Focus();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => Reload();

    private void DgProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dgProducts.SelectedItem is Product p) AddToCart(p);
    }

    private void BtnAddSelected_Click(object sender, RoutedEventArgs e)
    {
        if (dgProducts.SelectedItem is Product p) AddToCart(p);
        else MessageBox.Show("Hãy chọn một sản phẩm trong danh sách.", "Thông báo",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AddToCart(Product p)
    {
        if (p.Stock <= 0)
        {
            MessageBox.Show($"'{p.Name}' đã hết hàng.", "Hết hàng", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = _cart.FirstOrDefault(c => c.ProductId == p.Id);
        if (existing != null)
        {
            if (existing.Quantity >= p.Stock)
            {
                MessageBox.Show($"'{p.Name}' chỉ còn {p.Stock} trong kho.", "Không đủ hàng",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            existing.Quantity++;
        }
        else
        {
            var item = new CartItem
            {
                ProductId = p.Id,
                Barcode = p.Barcode,
                Name = p.Name,
                UnitPrice = p.Price,
                Stock = p.Stock,
                Quantity = 1
            };
            item.PropertyChanged += CartItem_PropertyChanged;
            _cart.Add(item);
        }

        UpdateTotals();
    }

    private void CartItem_PropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateTotals();

    // ---------------- Chỉnh sửa giỏ hàng ----------------

    private void BtnPlus_Click(object sender, RoutedEventArgs e)
    {
        if (dgCart.SelectedItem is CartItem item) item.Quantity++;
    }

    private void BtnMinus_Click(object sender, RoutedEventArgs e)
    {
        if (dgCart.SelectedItem is CartItem item) item.Quantity--;
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (dgCart.SelectedItem is CartItem item)
        {
            item.PropertyChanged -= CartItem_PropertyChanged;
            _cart.Remove(item);
            UpdateTotals();
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ClearCart();

    private void ClearCart()
    {
        foreach (var item in _cart) item.PropertyChanged -= CartItem_PropertyChanged;
        _cart.Clear();
        txtPaid.Clear();
        UpdateTotals();
        txtBarcode.Focus();
    }

    // ---------------- Tính tiền ----------------

    private decimal CurrentTotal => _cart.Sum(c => c.LineTotal);

    private void TxtPaid_TextChanged(object sender, TextChangedEventArgs e) => UpdateTotals();

    private void UpdateTotals()
    {
        decimal total = CurrentTotal;
        txtTotal.Text = MoneyParser.Format(total);

        if (MoneyParser.TryParse(txtPaid.Text, out decimal paid))
        {
            decimal change = OrderService.CalculateChange(total, paid);
            txtChange.Text = MoneyParser.Format(Math.Max(change, 0));
            txtChange.Foreground = change < 0 ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.Black;
        }
        else
        {
            txtChange.Text = "0 đ";
            txtChange.Foreground = System.Windows.Media.Brushes.Black;
        }
    }

    // ---------------- Thanh toán ----------------

    private void BtnPay_Click(object sender, RoutedEventArgs e)
    {
        // Kết thúc việc sửa ô số lượng (nếu đang gõ dở) để Quantity được cập nhật.
        dgCart.CommitEdit(DataGridEditingUnit.Row, true);

        if (_cart.Count == 0)
        {
            MessageBox.Show("Giỏ hàng đang trống.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        decimal total = CurrentTotal;

        // Để trống ô "Khách đưa" = khách trả đúng số tiền (ví dụ quẹt thẻ/chuyển khoản).
        decimal paid = total;
        if (!string.IsNullOrWhiteSpace(txtPaid.Text) && !MoneyParser.TryParse(txtPaid.Text, out paid))
        {
            MessageBox.Show("Số tiền khách đưa không hợp lệ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (paid < total)
        {
            MessageBox.Show($"Khách đưa chưa đủ. Còn thiếu {MoneyParser.Format(total - paid)}.", "Chưa đủ tiền",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var order = new Order
            {
                CashierId = AppServices.CurrentUser!.Id,
                CustomerPaid = paid
            };
            var details = _cart.Select(c => new OrderDetail
            {
                ProductId = c.ProductId,
                Quantity = c.Quantity,
                UnitPrice = c.UnitPrice
            }).ToList();

            var saved = AppServices.Orders.CreateOrder(order, details);

            MessageBox.Show(BuildReceipt(saved), "Thanh toán thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);

            ClearCart();
            Reload();
        }
        catch (BusinessException ex)
        {
            MessageBox.Show(ex.Message, "Không thể thanh toán", MessageBoxButton.OK, MessageBoxImage.Warning);
            Reload(); // tồn kho có thể đã thay đổi
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private string BuildReceipt(Order o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hóa đơn: {o.Code}");
        sb.AppendLine($"Thời gian: {o.CreatedAt:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"Thu ngân: {AppServices.CurrentUser?.FullName}");
        sb.AppendLine(new string('-', 40));
        foreach (var d in o.Details)
            sb.AppendLine($"{d.Product?.Name} x{d.Quantity}  =  {d.LineTotal:N0} đ");
        sb.AppendLine(new string('-', 40));
        sb.AppendLine($"Tổng tiền:  {o.Total:N0} đ");
        sb.AppendLine($"Khách đưa:  {o.CustomerPaid:N0} đ");
        sb.AppendLine($"Tiền thừa:  {o.Change:N0} đ");
        return sb.ToString();
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
