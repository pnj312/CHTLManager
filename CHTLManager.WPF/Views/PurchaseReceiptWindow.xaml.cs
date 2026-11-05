using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CHTLManager.BLL;
using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;
using CHTLManager.WPF.Models;

namespace CHTLManager.WPF.Views;

/// <summary>
/// Nhập một lô hàng theo hóa đơn của nhà cung cấp. Admin nhập cả giá và phiếu được xác nhận luôn;
/// thu ngân chỉ nhập số lượng, phiếu chờ Admin đối chiếu.
/// </summary>
public partial class PurchaseReceiptWindow : Window
{
    private const int MaxSuggestions = 8;

    private readonly ObservableCollection<ReceiptLineRow> _lines = new();
    private readonly Product? _initialProduct;
    private bool _saved;

    public PurchaseReceiptWindow(Product? initialProduct = null)
    {
        InitializeComponent();
        _initialProduct = initialProduct;
        dgLines.ItemsSource = _lines;
        DataGridHelper.EnableSingleClickEdit(dgLines);
        _lines.CollectionChanged += (_, _) => UpdateTotal();
        Title = AppServices.IsAdmin ? "Nhập hàng theo hóa đơn" : "Nhận hàng";
        if (!AppServices.IsAdmin)
        {
            colCost.Visibility = Visibility.Collapsed;
            txtTotal.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        cboSupplier.ItemsSource = AppServices.Purchases.GetSuppliers();
        cboSupplier.Focus();
        if (_initialProduct != null) AddProduct(_initialProduct);
    }

    private void TxtProduct_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true; // Enter ở ô này chỉ để thêm dòng, không lưu phiếu
                AddFromInput();
                break;
            case Key.Down:
                e.Handled = true;
                if (lstSuggestions.Visibility != Visibility.Visible) ShowSuggestions();
                if (lstSuggestions.Items.Count == 0) break;
                lstSuggestions.SelectedIndex = Math.Min(lstSuggestions.SelectedIndex + 1, lstSuggestions.Items.Count - 1);
                lstSuggestions.ScrollIntoView(lstSuggestions.SelectedItem);
                break;
            case Key.Up when lstSuggestions.Items.Count > 0:
                e.Handled = true;
                lstSuggestions.SelectedIndex = Math.Max(lstSuggestions.SelectedIndex - 1, 0);
                lstSuggestions.ScrollIntoView(lstSuggestions.SelectedItem);
                break;
            case Key.Escape when lstSuggestions.Visibility == Visibility.Visible:
                e.Handled = true; // Esc lúc này chỉ đóng danh sách, không đóng cả phiếu
                HideSuggestions();
                break;
        }
    }

    private void TxtProduct_TextChanged(object sender, TextChangedEventArgs e) => ShowSuggestions();

    private void ShowSuggestions()
    {
        string text = txtProduct.Text.Trim();
        if (text.Length == 0)
        {
            HideSuggestions();
            return;
        }

        var matches = AppServices.Products.Search(text).Take(MaxSuggestions).ToList();
        lstSuggestions.ItemsSource = matches;
        lstSuggestions.SelectedIndex = -1;
        lstSuggestions.Visibility = matches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LstSuggestions_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (lstSuggestions.SelectedItem is Product product) AddChosen(product);
    }

    private void HideSuggestions()
    {
        lstSuggestions.ItemsSource = null;
        lstSuggestions.Visibility = Visibility.Collapsed;
    }

    private void AddChosen(Product product)
    {
        txtError.Text = string.Empty;
        AddProduct(product);
        txtProduct.Clear();
        txtProduct.Focus();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e) => AddFromInput();

    private void AddFromInput()
    {
        txtError.Text = string.Empty;
        string text = txtProduct.Text.Trim();
        if (text.Length == 0) return;

        if (lstSuggestions.SelectedItem is Product chosen)
        {
            AddChosen(chosen);
            return;
        }

        var product = AppServices.Products.GetByBarcode(text);
        if (product == null)
        {
            var matches = AppServices.Products.Search(text);
            if (matches.Count == 1) product = matches[0];
            else
            {
                txtError.Text = matches.Count == 0
                    ? AppServices.IsAdmin
                        ? $"Không có sản phẩm '{text}' trong danh mục. Hãy lưu phiếu sau, tạo sản phẩm ở tab Quản lý Kho trước."
                        : $"Không có sản phẩm '{text}' trong danh mục. Để riêng hàng này và báo Admin tạo sản phẩm."
                    : $"Có {matches.Count} sản phẩm khớp '{text}'. Hãy bấm chọn trong danh sách, hoặc dùng phím mũi tên rồi Enter.";
                if (matches.Count > 0) ShowSuggestions(); // có thể đã đóng bằng Esc
                return;
            }
        }

        AddChosen(product);
    }

    private void AddProduct(Product product)
    {
        var existing = _lines.FirstOrDefault(l => l.Product.Id == product.Id);
        if (existing != null)
        {
            if (int.TryParse(existing.InvoicedText, out int quantity)) existing.InvoicedText = (quantity + 1).ToString();
            dgLines.SelectedItem = existing;
            return;
        }

        if (product.NeedsStockCheck)
        {
            product = SettleOutOfBook(product)!;
            if (product == null) return;
        }

        var row = new ReceiptLineRow { Product = product };
        // Giá gần nhất giúp đỡ phải gõ lại; dòng tổng tiền nhắc đối chiếu với hóa đơn. Thu ngân không được thấy giá.
        if (AppServices.IsAdmin && product.LastCost > 0) row.CostText = product.LastCost.ToString("N0");
        row.PropertyChanged += (_, _) => UpdateTotal();
        _lines.Add(row);
        dgLines.SelectedItem = row;
    }

    /// <summary>Hàng bán ngoài sổ thuộc lô trước: phải chốt bằng kiểm kê rồi mới nhập lô mới, để hai lô không lẫn.</summary>
    private Product? SettleOutOfBook(Product product)
    {
        string note = $"'{product.Name}' đã bán {-product.Stock} cái ngoài sổ từ lô trước.\n\n";
        MessageBox.Show(note + "Hãy kiểm kê hàng đang có trước khi nhập lô mới, để hai lô không bị lẫn. Lưu ý: đếm trước khi xếp hàng mới lên kệ.",
            "Cần kiểm kê trước", MessageBoxButton.OK, MessageBoxImage.Information);
        if (new StockCountWindow(product) { Owner = this }.ShowDialog() != true) return null;
        return AppServices.Products.GetById(product.Id);
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (dgLines.SelectedItem is ReceiptLineRow row) _lines.Remove(row);
    }

    private void UpdateTotal()
    {
        if (!AppServices.IsAdmin) return;
        if (_lines.Count == 0)
        {
            txtTotal.Text = string.Empty;
            return;
        }

        decimal total = 0;
        foreach (var row in _lines)
        {
            if (!int.TryParse(row.ReceivedText.Trim(), out int received) || !MoneyParser.TryParse(row.CostText, out decimal cost))
            {
                txtTotal.Text = $"'{row.ProductName}' chưa có số lượng hoặc giá nhập hợp lệ nên chưa tính được tổng tiền.";
                return;
            }
            total += received * cost;
        }
        txtTotal.Text = $"Tổng tiền theo số thực nhận: {MoneyParser.Format(total)}. Giá điền sẵn là giá nhập gần nhất: hãy đối chiếu với hóa đơn.";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_saved || _lines.Count == 0) return;

        var answer = MessageBox.Show(this, $"Phiếu đang có {_lines.Count} dòng chưa lưu. Đóng và bỏ phiếu này?", "Chưa lưu phiếu",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) e.Cancel = true;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        txtError.Text = string.Empty;
        dgLines.CommitEdit(DataGridEditingUnit.Row, true);

        if (string.IsNullOrWhiteSpace(cboSupplier.Text))
        {
            txtError.Text = "Hãy chọn hoặc nhập nhà cung cấp.";
            return;
        }

        var inputs = new List<ReceiptLineInput>();
        foreach (var row in _lines)
        {
            if (!int.TryParse(row.InvoicedText.Trim(), out int invoiced) || !int.TryParse(row.ReceivedText.Trim(), out int received))
            {
                txtError.Text = $"'{row.ProductName}': số lượng phải là số nguyên.";
                return;
            }

            decimal? cost = null;
            if (AppServices.IsAdmin)
            {
                if (!MoneyParser.TryParse(row.CostText, out decimal parsed))
                {
                    txtError.Text = $"'{row.ProductName}': giá nhập phải là số nguyên, ví dụ 7000 hoặc 7.000.";
                    return;
                }
                cost = parsed;
            }
            inputs.Add(new ReceiptLineInput(row.Product.Id, invoiced, received, cost));
        }

        try
        {
            string? supplierName = SupplierPrompt.Resolve(this, cboSupplier.Text);
            if (supplierName == null) return;

            var supplier = AppServices.Purchases.SaveSupplier(new Supplier { Name = supplierName });
            var saved = AppServices.Purchases.CreateReceipt(supplier.Id, txtInvoice.Text, inputs,
                AppServices.CurrentUser!.Id, AppServices.IsAdmin);

            string message = AppServices.IsAdmin
                ? $"Đã lưu phiếu {saved.Code}. Tiền hàng: {MoneyParser.Format(saved.Total)}."
                : $"Đã lưu phiếu {saved.Code}, tồn kho đã cộng. Giữ lại hóa đơn để Admin đối chiếu và nhập giá.";
            int shortQuantity = saved.Lines.Where(l => l.ShortQuantity > 0).Sum(l => l.ShortQuantity);
            if (shortQuantity > 0)
                message += $"\n\nNhận thiếu {shortQuantity} cái so với hóa đơn: hãy báo nhà cung cấp.";
            MessageBox.Show(message, "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            _saved = true;
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }
}
