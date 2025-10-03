using LB_FATE.Mobile.Services;

namespace LB_FATE.Mobile;

public partial class App : Application
{
    private DialogService? _dialogService;

    public App()
    {
        InitializeComponent();

        // 全局异常处理
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        base.OnStart();

        // 延迟获取DialogService，确保服务已注册
        Task.Run(async () =>
        {
            await Task.Delay(500);
            try
            {
                _dialogService = Handler?.MauiContext?.Services?.GetService<DialogService>();
            }
            catch { }
        });
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            HandleException(ex, "未处理的异常");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleException(e.Exception, "未观察到的Task异常");
        e.SetObserved(); // 防止应用崩溃
    }

    private void HandleException(Exception ex, string context)
    {
        var errorMessage = $"[{context}] {ex.GetType().Name}: {ex.Message}";
        var stackTrace = ex.StackTrace ?? "无堆栈信息";

        // 记录到调试输出
        System.Diagnostics.Debug.WriteLine($"========== {context} ==========");
        System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
        System.Diagnostics.Debug.WriteLine($"堆栈: {stackTrace}");
        System.Diagnostics.Debug.WriteLine("==========================================");

        // 显示错误弹窗
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                // 优先使用注入的DialogService
                if (_dialogService != null)
                {
                    await _dialogService.ShowErrorAsync(errorMessage, stackTrace);
                }
                else
                {
                    // 降级方案：使用系统弹窗
                    var fullError = $"{errorMessage}\n\n{stackTrace}";
                    var page = Windows?.FirstOrDefault()?.Page;
                    if (page != null)
                    {
                        var shouldCopy = await page.DisplayAlertAsync(
                            "⚠️ 应用程序错误",
                            $"{errorMessage}\n\n点击【确定】复制错误信息",
                            "确定",
                            "取消");

                        if (shouldCopy)
                        {
                            await Clipboard.SetTextAsync(fullError);
                        }
                    }
                }
            }
            catch (Exception displayEx)
            {
                // 如果弹窗也失败了，至少记录到调试输出
                System.Diagnostics.Debug.WriteLine($"显示错误弹窗失败: {displayEx.Message}");
            }
        });
    }
}