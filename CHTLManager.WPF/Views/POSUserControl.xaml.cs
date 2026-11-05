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
        DataGridHelper.EnableBarcodeCopy(dgProducts);
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
        var existing = _cart.FirstOrDefault(c => c.ProductId == p.Id);
        int inCart = existing?.Quantity ?? 0;
        bool confirmed = existing?.OversellConfirmed == true;

        // Kho báo không đủ: hỏi thu ngân hàng thực tế còn không, thay vì chặn bán.
        if (inCart + 1 > p.Stock && !confirmed)
        {
            if (!ConfirmOversell(p.Name, p.Stock)) return;
            confirmed = true;
        }

        if (existing != null)
        {
            if (confirmed) existing.OversellConfirmed = true;
            if (confirmed && existing.Quantity >= existing.MaxQuantity)
            {
                ShowOutOfBookLimit(existing.Name);
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
                OversellConfirmed = confirmed,
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
        if (dgCart.SelectedItem is not CartItem item)
        {
            ShowSelectCartRow();
            return;
        }

        if (item.Quantity >= item.Stock && !item.OversellConfirmed)
        {
            if (!ConfirmOversell(item.Name, item.Stock)) return;
            item.OversellConfirmed = true;
        }
        if (item.OversellConfirmed && item.Quantity >= item.MaxQuantity)
        {
            ShowOutOfBookLimit(item.Name);
            return;
        }
        item.Quantity++;
    }

    private static void ShowOutOfBookLimit(string name)
        => MessageBox.Show(
            $"'{name}' chỉ được bán ngoài sổ tối đa {OrderService.MaxOutOfBookPerLine} cái. Hãy nhận hàng hoặc kiểm kê trước khi bán thêm.",
            "Vượt giới hạn bán ngoài sổ", MessageBoxButton.OK, MessageBoxImage.Warning);

    /// <summary>
    /// Hỏi thu ngân khi kho trên phần mềm báo không đủ hàng. Mặc định là "No" để tránh bấm nhầm.
    /// </summary>
    private static bool ConfirmOversell(string name, int stock)
    {
        string message =
            $"Phần mềm ghi '{name}' chỉ còn {Math.Max(stock, 0)}, nhưng khách đang cầm sản phẩm này?\n\n" +
            "Có (Yes): bán bình thường, Admin sẽ được nhắc kiểm kê lại.\n" +
            "Không (No): không thêm vào giỏ.";
        return MessageBox.Show(message, "Kho báo không đủ hàng", MessageBoxButton.YesNo,
                   MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private void BtnMinus_Click(object sender, RoutedEventArgs e)
    {
        if (dgCart.SelectedItem is not CartItem item)
        {
            ShowSelectCartRow();
            return;
        }

        if (item.Quantity <= 1)
        {
            MessageBox.Show("Số lượng tối thiểu là 1. Muốn bỏ sản phẩm, bấm \"Xóa dòng\".", "Thông báo",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        item.Quantity--;
    }

    /// <summary>Kiểm tra số lượng gõ tay trong giỏ: phải là số nguyên ≥ 1; vượt tồn kho thì hỏi xác nhận.</summary>
    private void DgCart_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not CartItem item || e.EditingElement is not TextBox box) return;

        // Vượt tồn kho chưa xác nhận: giữ nguyên số cũ, rồi hỏi sau khi ô đã commit xong.
        if (int.TryParse(box.Text.Trim(), out int wanted) && wanted > item.Stock && !item.OversellConfirmed)
        {
            box.Text = item.Quantity.ToString();
            Dispatcher.BeginInvoke(() =>
            {
                if (!ConfirmOversell(item.Name, item.Stock)) return;
                item.OversellConfirmed = true;
                item.Quantity = wanted;
                if (wanted > item.MaxQuantity) ShowOutOfBookLimit(item.Name);
            });
            return;
        }

        if (wanted > item.MaxQuantity && item.OversellConfirmed)
        {
            box.Text = item.MaxQuantity.ToString();
            Dispatcher.BeginInvoke(() => ShowOutOfBookLimit(item.Name));
            return;
        }

        string? message = null;
        if (!int.TryParse(box.Text.Trim(), out int qty))
        {
            message = "Số lượng phải là số nguyên.";
            box.Text = item.Quantity.ToString();
        }
        else if (qty < 1)
        {
            message = "Số lượng tối thiểu là 1. Muốn bỏ sản phẩm, bấm \"Xóa dòng\".";
            box.Text = "1";
        }

        // Hiện thông báo sau khi DataGrid kết thúc chỉnh sửa, tránh mở hộp thoại giữa lúc đang commit ô.
        if (message != null)
            Dispatcher.BeginInvoke(() => MessageBox.Show(message, "Số lượng không hợp lệ",
                MessageBoxButton.OK, MessageBoxImage.Warning));
    }

    private static void ShowSelectCartRow()
        => MessageBox.Show("Hãy chọn một dòng trong giỏ hàng.", "Thông báo",
            MessageBoxButton.OK, MessageBoxImage.Information);

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

            var oversell = _cart.Where(c => c.OversellConfirmed).Select(c => c.ProductId).ToHashSet();
            var saved = AppServices.Orders.CreateOrder(order, details, oversell);

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

        var negative = o.Details.Where(d => d.Product is { Stock: < 0 }).Select(d => d.Product!).ToList();
        if (negative.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Lưu ý:");
            foreach (var p in negative)
                sb.AppendLine($"  • {p.Name}: đã bán {-p.Stock} cái ngoài sổ, cần kiểm kê");
        }
        return sb.ToString();
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
