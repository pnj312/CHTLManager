using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Admin đối chiếu phiếu do thu ngân nhận với hóa đơn và nhập giá.</summary>
public partial class PurchaseConfirmWindow : Window
{
    private sealed class Row
    {
        public int LineId { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Invoiced { get; init; }
        public int Received { get; init; }
        public string CostText { get; set; } = string.Empty;
    }

    private readonly PurchaseReceipt _receipt;
    private readonly List<Row> _rows;

    public PurchaseConfirmWindow(PurchaseReceipt receipt)
    {
        InitializeComponent();
        _receipt = receipt;
        txtHeader.Text = $"Phiếu {receipt.Code} – {receipt.Supplier?.Name}, hóa đơn {receipt.InvoiceNo}. Người nhận: {receipt.CreatedBy?.FullName}.";
        _rows = receipt.Lines.Select(l => new Row
        {
            LineId = l.Id,
            Name = l.Product?.Name ?? string.Empty,
            Invoiced = l.InvoicedQuantity,
            Received = l.ReceivedQuantity
        }).ToList();
        dgLines.ItemsSource = _rows;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        txtError.Text = string.Empty;
        dgLines.CommitEdit(DataGridEditingUnit.Row, true);

        var costs = new Dictionary<int, decimal>();
        foreach (var row in _rows)
        {
            if (!MoneyParser.TryParse(row.CostText, out decimal cost))
            {
                txtError.Text = $"'{row.Name}': giá nhập phải là số nguyên, ví dụ 7000 hoặc 7.000.";
                return;
            }
            costs[row.LineId] = cost;
        }

        try
        {
            var saved = AppServices.Purchases.ConfirmReceipt(_receipt.Id, costs, AppServices.CurrentUser!.Id);
            MessageBox.Show($"Đã xác nhận phiếu {saved.Code}. Tiền hàng: {MoneyParser.Format(saved.Total)}.", "Thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }
}
