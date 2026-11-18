using System.Net.Http;
using System.Windows;
using TopSolidMingdao.Core.Interfaces;
using TopSolidMingdao.Desktop.Configuration;
using TopSolidMingdao.Desktop.Diagnostics;
using TopSolidMingdao.Desktop.ViewModels;
using TopSolidMingdao.Infrastructure.Demo;
using TopSolidMingdao.Infrastructure.Mingdao;

namespace TopSolidMingdao.Desktop;

public partial class App : Application
{
    private MainViewModel? _viewModel;
    private HttpClient? _httpClient;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var settings = AppSettings.Load();
            // 认证请求不自动跟随重定向，避免将连接上下文发送到其他地址。
            _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            IMingdaoService service = settings.DataMode == "Demo"
                ? new DemoMingdaoService()
                : new MingdaoApiService(_httpClient, settings.Mingdao, MingdaoCredentials.FromEnvironment);
            var label = settings.DataMode == "Demo"
                ? "演示数据 · 未连接明道云" : $"{settings.Mingdao.DisplayName} · 只读查询";
            _viewModel = new MainViewModel(service, label, LocalLog.Write,
                drawingBrowsingEnabled: settings.DataMode == "Demo");
            MainWindow = new MainWindow { DataContext = _viewModel };
            MainWindow.Show();
            await _viewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            LocalLog.Write(exception);
            MessageBox.Show("程序启动失败。请检查程序目录中的 appsettings.json 和本地日志。",
                "TopSolid 明道云集成助手", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
