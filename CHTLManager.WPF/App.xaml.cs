using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using CHTLManager.BLL;
using CHTLManager.WPF.Views;

namespace CHTLManager.WPF;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Định dạng số/tiền theo chuẩn Việt Nam (1.025.845,26)
        var culture = new CultureInfo("vi-VN");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            DbInitializer.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không thể khởi tạo cơ sở dữ liệu:\n" + ex.Message,
                "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var login = new LoginWindow();
        if (login.ShowDialog() == true)
        {
            var main = new MainWindow();
            MainWindow = main;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            main.Show();
        }
        else
        {
            Shutdown();
        }
    }
}
