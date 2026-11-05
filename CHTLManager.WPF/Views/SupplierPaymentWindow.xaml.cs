using System.Windows;
using CHTLManager.BLL;
using CHTLManager.BLL.Interfaces;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Ghi một lần trả tiền cho nhà cung cấp; mặc định đề xuất trả hết số còn nợ.</summary>
public partial class SupplierPaymentWindow : Window
{
    private readonly SupplierBalance _balance;

    public SupplierPaymentWindow(SupplierBalance balance)
    {
        InitializeComponent();
        _balance = balance;
        txtHeader.Text = $"{balance.Supplier.Name}: còn nợ {MoneyParser.Format(balance.Balance)}";
        txtAmount.Text = balance.Balance.ToString("0");
        Loaded += (_, _) => { txtAmount.Focus(); txtAmount.SelectAll(); };
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        txtError.Text = string.Empty;
        if (!MoneyParser.TryParse(txtAmount.Text, out decimal amount))
        {
            txtError.Text = "Số tiền phải là số nguyên, ví dụ 500000 hoặc 500.000.";
            return;
        }

        try
        {
            AppServices.Purchases.PaySupplier(_balance.Supplier.Id, amount, txtNote.Text, AppServices.CurrentUser!.Id);
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }
}
