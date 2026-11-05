using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Admin quyết định phần chênh lệch đi về đâu; chỉ hiện cách xử lý đúng chiều thiếu hay thừa.</summary>
public partial class DiscrepancyResolveWindow : Window
{
    private sealed record Option(string Label, DiscrepancyResolution Resolution, string? NotePrefix = null)
    {
        public override string ToString() => Label;
    }

    private readonly StockDiscrepancy _discrepancy;

    public DiscrepancyResolveWindow(StockDiscrepancy discrepancy)
    {
        InitializeComponent();
        _discrepancy = discrepancy;

        string name = discrepancy.Product?.Name ?? string.Empty;
        txtHeader.Text = discrepancy.IsShortage
            ? $"{name}: thiếu {-discrepancy.Delta} cái so với sổ, giá trị {MoneyParser.Format(discrepancy.Value)} theo giá vốn."
            : $"{name}: thừa {discrepancy.Delta} cái so với sổ.";

        cboResolution.ItemsSource = discrepancy.IsShortage
            ? new[]
            {
                new Option("Mất, chưa rõ người chịu trách nhiệm (tính vào chi phí)", DiscrepancyResolution.NormalLoss, "Mất"),
                new Option("Hỏng (tính vào chi phí)", DiscrepancyResolution.NormalLoss, "Hỏng"),
                new Option("Hết hạn (tính vào chi phí)", DiscrepancyResolution.NormalLoss, "Hết hạn"),
                new Option("Có người chịu trách nhiệm bồi thường", DiscrepancyResolution.Compensation),
                new Option("Đếm sai trước đó (phải ghi chú)", DiscrepancyResolution.Miscount)
            }
            : new[]
            {
                new Option("Lô hàng chưa ghi (tạo phiếu nhập, sinh công nợ)", DiscrepancyResolution.UnrecordedPurchase),
                new Option("Không rõ nguồn (ghi thu nhập khác)", DiscrepancyResolution.OtherIncome),
                new Option("Đếm sai trước đó (phải ghi chú)", DiscrepancyResolution.Miscount)
            };

        cboResponsible.ItemsSource = AppServices.Users.GetAll().Where(u => u.IsActive).ToList();
        cboSupplier.ItemsSource = AppServices.Purchases.GetSuppliers();
        txtCompensation.Text = discrepancy.Value.ToString("0");
        decimal lastCost = discrepancy.Product?.LastCost ?? 0;
        if (lastCost > 0) txtCost.Text = lastCost.ToString("0");
    }

    private void CboResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        txtError.Text = string.Empty;
        var option = cboResolution.SelectedItem as Option;
        pnlCompensation.Visibility = option?.Resolution == DiscrepancyResolution.Compensation ? Visibility.Visible : Visibility.Collapsed;
        pnlPurchase.Visibility = option?.Resolution == DiscrepancyResolution.UnrecordedPurchase ? Visibility.Visible : Visibility.Collapsed;
        lblNote.Text = option?.Resolution == DiscrepancyResolution.Miscount ? "Ghi chú * (giải thích vì sao đếm sai)" : "Ghi chú";
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        txtError.Text = string.Empty;
        if (cboResolution.SelectedItem is not Option option)
        {
            txtError.Text = "Hãy chọn cách xử lý.";
            return;
        }

        string? note = string.IsNullOrWhiteSpace(txtNote.Text) ? null : txtNote.Text.Trim();
        if (option.NotePrefix != null) note = note == null ? option.NotePrefix : $"{option.NotePrefix}: {note}";
        int adminId = AppServices.CurrentUser!.Id;

        try
        {
            if (_discrepancy.IsShortage) SaveShortage(option, note, adminId);
            else if (!SaveSurplus(option, note, adminId)) return;
            AppServices.NotifyChanged();
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }

    private void SaveShortage(Option option, string? note, int adminId)
    {
        decimal? amount = null;
        int? responsibleId = null;
        string? responsibleName = null;
        if (option.Resolution == DiscrepancyResolution.Compensation)
        {
            if (!MoneyParser.TryParse(txtCompensation.Text, out decimal parsed))
                throw new BusinessException("Số tiền bồi thường phải là số nguyên, ví dụ 50000 hoặc 50.000.");
            amount = parsed;

            // Ô cho phép gõ tên người không có tài khoản; chỉ coi là tài khoản khi chữ khớp đúng mục đã chọn.
            if (cboResponsible.SelectedItem is User user && user.FullName == cboResponsible.Text) responsibleId = user.Id;
            else responsibleName = cboResponsible.Text;
        }
        AppServices.StockCounts.ResolveShortage(_discrepancy.Id, option.Resolution, note, responsibleId, responsibleName, amount, adminId);
    }

    private bool SaveSurplus(Option option, string? note, int adminId)
    {
        int? supplierId = null;
        decimal? cost = null;
        string? invoice = null;
        if (option.Resolution == DiscrepancyResolution.UnrecordedPurchase)
        {
            if (!MoneyParser.TryParse(txtCost.Text, out decimal parsed))
            {
                txtError.Text = "Giá nhập phải là số nguyên, ví dụ 7000 hoặc 7.000.";
                return false;
            }
            cost = parsed;
            invoice = txtInvoice.Text;
            supplierId = AppServices.Purchases.SaveSupplier(new Supplier { Name = cboSupplier.Text }).Id;
        }
        AppServices.StockCounts.ResolveSurplus(_discrepancy.Id, option.Resolution, note, supplierId, invoice, cost, adminId);
        return true;
    }
}
