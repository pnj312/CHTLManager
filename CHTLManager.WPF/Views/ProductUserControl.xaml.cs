using System;
using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL.Services;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Views
{
    public partial class ProductUserControl : UserControl
    {
        private readonly ProductService _productService = new ProductService();
        private Product? _selectedProduct;

        public ProductUserControl()
        {
            InitializeComponent();
        }

        private void ProductUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProducts();
        }

        private void ProductSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ProductMessageTextBlock.Text = string.Empty;
                LoadProducts();
            }
        }

        private bool LoadProducts()
        {
            try
            {
                string keyword = ProductSearchTextBox.Text.Trim();
                ProductDataGrid.ItemsSource = string.IsNullOrEmpty(keyword)
                    ? _productService.GetAll()
                    : _productService.Search(keyword);
                return true;
            }
            catch (Exception)
            {
                ProductDataGrid.ItemsSource = null;
                ShowMessage("Không thể tải dữ liệu sản phẩm.", true);
                return false;
            }
        }

        private void ProductDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProductDataGrid.SelectedItem is not Product product)
            {
                _selectedProduct = null;
                return;
            }

            _selectedProduct = product;
            BarcodeTextBox.Text = product.Barcode;
            ProductNameTextBox.Text = product.Name;
            ImportPriceTextBox.Text = product.ImportPrice.ToString();
            SellingPriceTextBox.Text = product.SellingPrice.ToString();
            StockTextBox.Text = product.StockQuantity.ToString();
            UnitTextBox.Text = product.Unit;
            ProductMessageTextBlock.Text = string.Empty;
        }

        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            ProductMessageTextBlock.Text = string.Empty;
            if (!TryReadProduct(out Product product))
            {
                return;
            }

            try
            {
                if (!_productService.Add(product))
                {
                    ShowMessage("Không thể thêm sản phẩm. Vui lòng kiểm tra dữ liệu hoặc mã vạch đã tồn tại.", true);
                    return;
                }
            }
            catch (Exception)
            {
                ShowMessage("Không thể lưu sản phẩm. Vui lòng thử lại.", true);
                return;
            }

            ClearForm();
            if (LoadProducts())
            {
                ShowMessage("Thêm sản phẩm thành công.", false);
            }
        }

        private void UpdateProductButton_Click(object sender, RoutedEventArgs e)
        {
            ProductMessageTextBlock.Text = string.Empty;
            if (_selectedProduct is null)
            {
                ShowMessage("Vui lòng chọn sản phẩm cần sửa.", true);
                return;
            }

            if (!TryReadProduct(out Product product))
            {
                return;
            }

            product.Id = _selectedProduct.Id;
            try
            {
                if (!_productService.Update(product))
                {
                    ShowMessage("Không thể cập nhật sản phẩm. Vui lòng kiểm tra dữ liệu hoặc mã vạch.", true);
                    return;
                }
            }
            catch (Exception)
            {
                ShowMessage("Không thể lưu sản phẩm. Vui lòng thử lại.", true);
                return;
            }

            ClearForm();
            if (LoadProducts())
            {
                ShowMessage("Cập nhật sản phẩm thành công.", false);
            }
        }

        private void DeleteProductButton_Click(object sender, RoutedEventArgs e)
        {
            ProductMessageTextBlock.Text = string.Empty;
            if (_selectedProduct is null)
            {
                ShowMessage("Vui lòng chọn sản phẩm cần xóa.", true);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                "Bạn có chắc muốn xóa sản phẩm này?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                if (!_productService.Delete(_selectedProduct.Id))
                {
                    ShowMessage("Không thể xóa sản phẩm. Vui lòng thử lại.", true);
                    return;
                }
            }
            catch (Exception)
            {
                ShowMessage("Không thể xóa sản phẩm vì sản phẩm có thể đã được sử dụng trong hóa đơn hoặc cơ sở dữ liệu đang gặp lỗi.", true);
                return;
            }

            ClearForm();
            if (LoadProducts())
            {
                ShowMessage("Xóa sản phẩm thành công.", false);
            }
        }

        private bool TryReadProduct(out Product product)
        {
            product = new Product();
            string barcode = BarcodeTextBox.Text.Trim();
            string name = ProductNameTextBox.Text.Trim();
            string unit = UnitTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(barcode))
            {
                ShowMessage("Vui lòng nhập mã vạch.", true);
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowMessage("Vui lòng nhập tên sản phẩm.", true);
                return false;
            }

            if (!decimal.TryParse(ImportPriceTextBox.Text.Trim(), out decimal importPrice) || importPrice < 0)
            {
                ShowMessage("Giá nhập phải là số không âm hợp lệ.", true);
                return false;
            }

            if (!decimal.TryParse(SellingPriceTextBox.Text.Trim(), out decimal sellingPrice) || sellingPrice < 0)
            {
                ShowMessage("Giá bán phải là số không âm hợp lệ.", true);
                return false;
            }

            if (!int.TryParse(StockTextBox.Text.Trim(), out int stockQuantity) || stockQuantity < 0)
            {
                ShowMessage("Số lượng tồn phải là số nguyên không âm hợp lệ.", true);
                return false;
            }

            if (string.IsNullOrWhiteSpace(unit))
            {
                ShowMessage("Vui lòng nhập đơn vị tính.", true);
                return false;
            }

            product = new Product
            {
                Barcode = barcode,
                Name = name,
                ImportPrice = importPrice,
                SellingPrice = sellingPrice,
                StockQuantity = stockQuantity,
                Unit = unit
            };
            return true;
        }

        private void ClearForm()
        {
            _selectedProduct = null;
            ProductDataGrid.SelectedItem = null;
            BarcodeTextBox.Clear();
            ProductNameTextBox.Clear();
            ImportPriceTextBox.Clear();
            SellingPriceTextBox.Clear();
            StockTextBox.Clear();
            UnitTextBox.Clear();
        }

        private void ShowMessage(string message, bool isError)
        {
            ProductMessageTextBlock.Text = message;
            ProductMessageTextBlock.SetResourceReference(
                TextBlock.ForegroundProperty,
                isError ? "AppDangerBrush" : "AppPrimaryBrush");
        }
    }
}
