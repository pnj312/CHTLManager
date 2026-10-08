using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CHTLManager.BLL.Services;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Models;

namespace CHTLManager.WPF.Views
{
    public partial class POSUserControl : UserControl
    {
        private readonly ProductService _productService = new ProductService();
        private readonly OrderService _orderService = new OrderService();
        private readonly ObservableCollection<CartItem> _cartItems = new ObservableCollection<CartItem>();
        private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
        private User? _currentUser;
        private bool _checkoutInProgress;

        public POSUserControl()
        {
            InitializeComponent();
            CartDataGrid.ItemsSource = _cartItems;
        }

        public void SetCurrentUser(User user)
        {
            _currentUser = user;
        }

        private void POSUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProducts();
        }

        private void LoadProducts()
        {
            try
            {
                var products = _productService.GetAll();
                AvailableProductsDataGrid.ItemsSource = products;

                bool cartAdjusted = false;
                foreach (CartItem item in _cartItems.ToList())
                {
                    Product? currentProduct = products.FirstOrDefault(product => product.Id == item.ProductId);
                    if (currentProduct is null || currentProduct.StockQuantity <= 0)
                    {
                        _cartItems.Remove(item);
                        cartAdjusted = true;
                        continue;
                    }

                    item.AvailableStock = currentProduct.StockQuantity;
                    if (item.Name != currentProduct.Name || item.Barcode != currentProduct.Barcode ||
                        item.UnitPrice != currentProduct.SellingPrice)
                    {
                        item.Name = currentProduct.Name;
                        item.Barcode = currentProduct.Barcode;
                        item.UnitPrice = currentProduct.SellingPrice;
                        cartAdjusted = true;
                    }
                    if (item.Quantity > item.AvailableStock)
                    {
                        item.Quantity = item.AvailableStock;
                        cartAdjusted = true;
                    }
                }

                if (cartAdjusted)
                {
                    RefreshCart();
                    ShowMessage("Giỏ hàng đã được cập nhật theo thông tin sản phẩm hiện tại.", false);
                }
            }
            catch (Exception)
            {
                AvailableProductsDataGrid.ItemsSource = null;
                ShowMessage("Không thể tải danh sách sản phẩm.", true);
            }
        }

        private void ProductLookupTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded && string.IsNullOrWhiteSpace(ProductLookupTextBox.Text))
            {
                LoadProducts();
            }
        }

        private void ProductLookupTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                LookupProduct();
            }
        }

        private void SearchProductButton_Click(object sender, RoutedEventArgs e)
        {
            LookupProduct();
        }

        private void LookupProduct()
        {
            POSMessageTextBlock.Text = string.Empty;
            string keyword = ProductLookupTextBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                LoadProducts();
                return;
            }

            try
            {
                Product? product = _productService.GetByBarcode(keyword);
                if (product is not null && string.Equals(product.Barcode, keyword, StringComparison.Ordinal))
                {
                    if (AddToCart(product))
                    {
                        ProductLookupTextBox.Clear();
                    }
                    return;
                }

                var results = _productService.Search(keyword);
                AvailableProductsDataGrid.ItemsSource = results;
                if (results.Count == 0)
                {
                    ShowMessage("Không tìm thấy sản phẩm.", true);
                }
            }
            catch (Exception)
            {
                ShowMessage("Không thể tìm sản phẩm. Vui lòng thử lại.", true);
            }
        }

        private void AvailableProductsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(AvailableProductsDataGrid, source) is DataGridRow row &&
                row.Item is Product product)
            {
                POSMessageTextBlock.Text = string.Empty;
                AddToCart(product);
            }
        }

        private bool AddToCart(Product product)
        {
            if (product.StockQuantity <= 0)
            {
                ShowMessage("Sản phẩm đã hết hàng.", true);
                return false;
            }

            bool wasEmpty = _cartItems.Count == 0;
            CartItem? existing = _cartItems.FirstOrDefault(item => item.ProductId == product.Id);
            if (existing is not null)
            {
                if (existing.Quantity >= product.StockQuantity)
                {
                    ShowMessage("Số lượng trong giỏ vượt quá tồn kho.", true);
                    return false;
                }

                existing.AvailableStock = product.StockQuantity;
                existing.Quantity++;
            }
            else
            {
                _cartItems.Add(new CartItem
                {
                    ProductId = product.Id,
                    Barcode = product.Barcode,
                    Name = product.Name,
                    UnitPrice = product.SellingPrice,
                    Quantity = 1,
                    AvailableStock = product.StockQuantity
                });
            }

            if (wasEmpty)
            {
                ChangeAmountTextBlock.Text = FormatMoney(0);
            }

            RefreshCart();
            ShowMessage("Đã thêm sản phẩm vào giỏ.", false);
            return true;
        }

        private void IncreaseQuantityButton_Click(object sender, RoutedEventArgs e)
        {
            POSMessageTextBlock.Text = string.Empty;
            if (CartDataGrid.SelectedItem is not CartItem item)
            {
                ShowMessage("Vui lòng chọn sản phẩm trong giỏ hàng.", true);
                return;
            }

            if (item.Quantity >= item.AvailableStock)
            {
                ShowMessage("Số lượng trong giỏ vượt quá tồn kho.", true);
                return;
            }

            item.Quantity++;
            RefreshCart();
        }

        private void DecreaseQuantityButton_Click(object sender, RoutedEventArgs e)
        {
            POSMessageTextBlock.Text = string.Empty;
            if (CartDataGrid.SelectedItem is not CartItem item)
            {
                ShowMessage("Vui lòng chọn sản phẩm trong giỏ hàng.", true);
                return;
            }

            item.Quantity--;
            if (item.Quantity == 0)
            {
                _cartItems.Remove(item);
            }

            RefreshCart();
        }

        private void RemoveCartItemButton_Click(object sender, RoutedEventArgs e)
        {
            POSMessageTextBlock.Text = string.Empty;
            if (CartDataGrid.SelectedItem is not CartItem item)
            {
                ShowMessage("Vui lòng chọn sản phẩm trong giỏ hàng.", true);
                return;
            }

            _cartItems.Remove(item);
            RefreshCart();
        }

        private void ClearCartButton_Click(object sender, RoutedEventArgs e)
        {
            POSMessageTextBlock.Text = string.Empty;
            if (_cartItems.Count == 0)
            {
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                "Bạn có chắc muốn xóa toàn bộ giỏ hàng?",
                "Xác nhận hủy giỏ",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            _cartItems.Clear();
            RefreshCart();
            CustomerPaymentTextBox.Clear();
            ChangeAmountTextBlock.Text = FormatMoney(0);
            ShowMessage("Đã xóa toàn bộ giỏ hàng.", false);
        }

        private void CheckoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_checkoutInProgress)
            {
                return;
            }

            POSMessageTextBlock.Text = string.Empty;
            if (_cartItems.Count == 0)
            {
                ShowMessage("Giỏ hàng đang trống.", true);
                return;
            }

            if (_currentUser is null)
            {
                ShowMessage("Không xác định được người dùng đăng nhập.", true);
                return;
            }

            string paymentText = CustomerPaymentTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(paymentText))
            {
                ShowMessage("Vui lòng nhập số tiền khách đưa.", true);
                return;
            }

            if (!decimal.TryParse(paymentText, NumberStyles.Number, VietnameseCulture, out decimal customerPayment) || customerPayment < 0)
            {
                ShowMessage("Số tiền khách đưa không hợp lệ.", true);
                return;
            }

            var orderRequest = new Order
            {
                UserId = _currentUser.Id,
                CustomerPayment = customerPayment
            };
            List<OrderDetail> detailRequests = _cartItems.Select(item => new OrderDetail
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity
            }).ToList();

            _checkoutInProgress = true;
            CheckoutButton.IsEnabled = false;
            try
            {
                Order? result;
                try
                {
                    result = _orderService.CreateOrder(orderRequest, detailRequests);
                }
                catch (Exception)
                {
                    ShowMessage("Không thể hoàn tất thanh toán. Vui lòng thử lại.", true);
                    return;
                }

                if (result is null)
                {
                    ShowMessage("Thanh toán không thành công. Vui lòng kiểm tra số tiền và tồn kho.", true);
                    return;
                }

                _cartItems.Clear();
                CartDataGrid.SelectedItem = null;
                RefreshCart();
                CustomerPaymentTextBox.Clear();
                ProductLookupTextBox.Clear();
                LoadProducts();
                ChangeAmountTextBlock.Text = FormatMoney(result.ChangeAmount);
                ShowMessage(
                    $"Thanh toán thành công. Hóa đơn #{result.Id} lúc {result.CreatedAt:dd/MM/yyyy HH:mm}. " +
                    $"Tổng: {FormatMoney(result.TotalAmount)}. Tiền thừa: {FormatMoney(result.ChangeAmount)}.",
                    false);
            }
            finally
            {
                CheckoutButton.IsEnabled = true;
                _checkoutInProgress = false;
            }
        }

        private void RefreshCart()
        {
            CartDataGrid.Items.Refresh();
            decimal total = _cartItems.Sum(item => item.LineTotal);
            TotalAmountTextBlock.Text = FormatMoney(total);
        }

        private static string FormatMoney(decimal amount)
        {
            return $"{amount.ToString("#,##0.##", VietnameseCulture)} ₫";
        }

        private void ShowMessage(string message, bool isError)
        {
            POSMessageTextBlock.Text = message;
            POSMessageTextBlock.SetResourceReference(
                TextBlock.ForegroundProperty,
                isError ? "AppDangerBrush" : "AppPrimaryBrush");
        }
    }
}
