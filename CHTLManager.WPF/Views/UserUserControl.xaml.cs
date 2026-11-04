using System.Windows;
using System.Windows.Controls;
using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Views;

/// <summary>Màn hình quản lý tài khoản (chỉ Admin): tạo, khóa/mở khóa, đặt lại mật khẩu.</summary>
public partial class UserUserControl : UserControl
{
    public UserUserControl()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => Reload(null);

    private void Reload(int? selectId)
    {
        try
        {
            var users = AppServices.Users.GetAll();
            dgUsers.ItemsSource = users;
            var again = selectId.HasValue ? users.FirstOrDefault(u => u.Id == selectId.Value) : null;
            if (again != null) dgUsers.SelectedItem = again;
            else ShowSelected(null);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void DgUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowSelected(dgUsers.SelectedItem as User);
        e.Handled = true; // không để sự kiện nổi lên TabControl của cửa sổ chính
    }

    private void ShowSelected(User? u)
    {
        txtResetPassword.Clear();
        if (u == null)
        {
            txtSelected.Text = "Chọn một tài khoản trong danh sách";
            btnToggleActive.Content = "Khóa / Mở khóa";
            return;
        }

        txtSelected.Text = $"Tài khoản đang chọn: {u.Username} ({u.FullName}) – {u.StatusName}";
        btnToggleActive.Content = u.IsActive ? "Khóa tài khoản" : "Mở khóa tài khoản";
    }

    private void BtnCreate_Click(object sender, RoutedEventArgs e)
    {
        var role = cboNewRole.SelectedIndex == 1 ? UserRole.Admin : UserRole.Cashier;
        try
        {
            var created = AppServices.Users.Create(txtNewUsername.Text, txtNewPassword.Password, txtNewFullName.Text, role);
            MessageBox.Show($"Đã tạo tài khoản '{created.Username}' ({created.RoleName}).", "Thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
            txtNewUsername.Clear();
            txtNewFullName.Clear();
            txtNewPassword.Clear();
            cboNewRole.SelectedIndex = 0;
            Reload(created.Id);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void BtnToggleActive_Click(object sender, RoutedEventArgs e)
    {
        if (dgUsers.SelectedItem is not User u)
        {
            ShowWarning("Hãy chọn tài khoản trong danh sách.");
            return;
        }

        string action = u.IsActive ? "Khóa" : "Mở khóa";
        if (MessageBox.Show($"{action} tài khoản '{u.Username}'?", "Xác nhận",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            AppServices.Users.SetActive(u.Id, !u.IsActive, AppServices.CurrentUser!.Id);
            Reload(u.Id);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void BtnResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (dgUsers.SelectedItem is not User u)
        {
            ShowWarning("Hãy chọn tài khoản trong danh sách.");
            return;
        }

        try
        {
            AppServices.Users.ResetPassword(u.Id, txtResetPassword.Password);
            txtResetPassword.Clear();
            MessageBox.Show($"Đã đặt lại mật khẩu cho '{u.Username}'.", "Thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (BusinessException ex) { ShowWarning(ex.Message); }
        catch (Exception ex) { ShowError(ex); }
    }

    private static void ShowWarning(string message)
        => MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static void ShowError(Exception ex)
        => MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
}
