using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LB_FATE.Mobile.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        // 添加全局异常处理
        this.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // 记录详细异常信息
        System.Diagnostics.Debug.WriteLine("=== 未处理的异常 ===");
        System.Diagnostics.Debug.WriteLine($"异常类型: {e.Exception.GetType().Name}");
        System.Diagnostics.Debug.WriteLine($"异常消息: {e.Exception.Message}");
        System.Diagnostics.Debug.WriteLine($"堆栈跟踪:\n{e.Exception.StackTrace}");

        if (e.Exception.InnerException != null)
        {
            System.Diagnostics.Debug.WriteLine($"内部异常: {e.Exception.InnerException.Message}");
            System.Diagnostics.Debug.WriteLine($"内部异常堆栈:\n{e.Exception.InnerException.StackTrace}");
        }

        System.Diagnostics.Debug.WriteLine("===================");

        // 尝试显示用户友好的错误消息
        try
        {
            var window = Microsoft.Maui.MauiWinUIApplication.Current?.Application?.Windows?.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window?.Content != null)
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "应用程序错误",
                    Content = $"检测到未处理的异常，应用程序将继续运行。\n\n异常类型: {e.Exception.GetType().Name}\n异常信息: {e.Exception.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = window.Content.XamlRoot
                };

                _ = dialog.ShowAsync();
            }
        }
        catch
        {
            // 如果显示对话框失败，至少已经记录了日志
        }

        // 标记为已处理，防止应用崩溃
        e.Handled = true;
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        // 延迟配置窗口，避免在窗口未完全初始化时访问
        _ = ConfigureWindowAsync();
    }

    private async System.Threading.Tasks.Task ConfigureWindowAsync()
    {
        try
        {
            // 等待窗口完全加载
            await System.Threading.Tasks.Task.Delay(500);

            // 在UI线程上配置窗口
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dispatcher == null)
            {
                System.Diagnostics.Debug.WriteLine("无法获取Dispatcher");
                return;
            }

            dispatcher.TryEnqueue(() =>
            {
                try
                {
                    ConfigureWindow();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"配置窗口异常: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ConfigureWindowAsync异常: {ex}");
        }
    }

    private void ConfigureWindow()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("开始配置窗口...");

            // Get the main window
            var window = Microsoft.Maui.MauiWinUIApplication.Current?.Application?.Windows?.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

            if (window == null)
            {
                System.Diagnostics.Debug.WriteLine("警告: 无法获取主窗口，跳过配置");
                return;
            }

            System.Diagnostics.Debug.WriteLine("成功获取主窗口");

            // Set window title
            window.Title = "应用程序错误";
            System.Diagnostics.Debug.WriteLine("设置窗口标题完成");

            // 获取AppWindow
            var appWindow = GetAppWindow(window);
            if (appWindow == null)
            {
                System.Diagnostics.Debug.WriteLine("警告: 无法获取AppWindow，跳过高级配置");
                return;
            }

            System.Diagnostics.Debug.WriteLine("成功获取AppWindow");

            // 设置窗口大小
            try
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
                System.Diagnostics.Debug.WriteLine("设置窗口大小完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置窗口大小失败: {ex.Message}");
            }

            // 设置窗口可调整大小
            try
            {
                var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.IsResizable = true;
                    presenter.IsMaximizable = true;
                    presenter.IsMinimizable = true;
                    System.Diagnostics.Debug.WriteLine("设置窗口属性完成");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置窗口属性失败: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("窗口配置完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"配置窗口失败: {ex}");
        }
    }

    private Microsoft.UI.Windowing.AppWindow? GetAppWindow(Microsoft.UI.Xaml.Window window)
    {
        try
        {
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取AppWindow失败: {ex.Message}");
            return null;
        }
    }
}







