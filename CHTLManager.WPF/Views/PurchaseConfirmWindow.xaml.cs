using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Admin đối chiếu phiếu do thu ngân nhận với hóa đơn và nhập giá, hoặc sửa giá của phiếu đã xác nhận.</summary>
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
    private readonly bool _correcting;

    public PurchaseConfirmWindow(PurchaseReceipt receipt, bool correcting = false)
    {
        InitializeComponent();
        DataGridHelper.EnableSingleClickEdit(dgLines);
        _receipt = receipt;
        _correcting = correcting;
        txtHeader.Text = $"Phiếu {receipt.Code} – {receipt.Supplier?.Name}, hóa đơn {receipt.InvoiceNo}. Người nhận: {receipt.CreatedBy?.FullName}.";
        _rows = receipt.Lines.Select(l => new Row
        {
            LineId = l.Id,
            Name = l.Product?.Name ?? string.Empty,
            Invoiced = l.InvoicedQuantity,
            Received = l.ReceivedQuantity,
            CostText = correcting ? (l.UnitCost ?? 0).ToString("N0") : string.Empty
        }).ToList();
        dgLines.ItemsSource = _rows;

        if (correcting)
        {
            Title = "Sửa giá nhập";
            btnSave.Content = "Lưu giá mới";
            btnLater.Content = "Đóng";
            txtHeader.Text += " Công nợ và giá vốn sẽ đổi theo giá mới, tồn kho giữ nguyên. Lãi của các hóa đơn đã bán không đổi.";
        }
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

        if (_correcting)
        {
            SaveCorrection(costs);
            return;
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

    private void SaveCorrection(Dictionary<int, decimal> costs)
    {
        string? reason = ReasonWindow.Ask(this, "Lý do sửa giá", $"Vì sao sửa giá phiếu {_receipt.Code}?");
        if (reason == null) return;

        try
        {
            AppServices.Purchases.CorrectReceiptCosts(_receipt.Id, costs, reason, AppServices.CurrentUser!.Id);
            MessageBox.Show($"Đã sửa giá phiếu {_receipt.Code}.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }
}
