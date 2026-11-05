using System.Windows;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;
using CHTLManager.WPF.Helpers;

namespace CHTLManager.WPF.Views;

/// <summary>Admin sửa người chịu trách nhiệm hoặc số tiền của khoản bồi thường ghi nhầm.</summary>
public partial class CompensationEditWindow : Window
{
    private readonly StockDiscrepancy _discrepancy;

    public CompensationEditWindow(StockDiscrepancy discrepancy)
    {
        InitializeComponent();
        _discrepancy = discrepancy;

        txtHeader.Text = $"{discrepancy.Product?.Name}: đã thu {MoneyParser.Format(discrepancy.CompensationCollected)}. "
                         + "Muốn đổi người thì phải hủy các lần thu của người cũ trước.";

        // Giữ cả tài khoản đã khóa nếu đang là người chịu trách nhiệm, để không biến thành tên gõ tay.
        var users = AppServices.Users.GetAll().Where(u => u.IsActive || u.Id == discrepancy.ResponsibleUserId).ToList();
        cboResponsible.ItemsSource = users;
        cboResponsible.SelectedItem = users.FirstOrDefault(u => u.Id == discrepancy.ResponsibleUserId);
        if (cboResponsible.SelectedItem == null) cboResponsible.Text = discrepancy.ResponsibleName ?? string.Empty;
        txtAmount.Text = (discrepancy.CompensationAmount ?? 0).ToString("0");
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        txtError.Text = string.Empty;
        if (!MoneyParser.TryParse(txtAmount.Text, out decimal amount))
        {
            txtError.Text = "Số tiền bồi thường phải là số nguyên, ví dụ 50000 hoặc 50.000.";
            return;
        }

        // Ô cho phép gõ tên người không có tài khoản; chỉ coi là tài khoản khi chữ khớp đúng mục đã chọn.
        int? userId = null;
        string? name = null;
        if (cboResponsible.SelectedItem is User user && user.FullName == cboResponsible.Text) userId = user.Id;
        else name = cboResponsible.Text;

        try
        {
            AppServices.StockCounts.CorrectCompensation(_discrepancy.Id, userId, name, amount, txtReason.Text, AppServices.CurrentUser!.Id);
            DialogResult = true;
        }
        catch (BusinessException ex) { txtError.Text = ex.Message; }
        catch (Exception ex) { txtError.Text = "Lỗi hệ thống: " + ex.Message; }
    }
}
