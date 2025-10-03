using Android.App;
using Android.Runtime;

namespace LB_FATE.Mobile;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        // 添加全局异常处理
        AndroidEnvironment.UnhandledExceptionRaiser += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnUnhandledException(object? sender, RaiseThrowableEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("=== Android 未处理的异常 ===");
        System.Diagnostics.Debug.WriteLine($"异常: {e.Exception}");
        System.Diagnostics.Debug.WriteLine("========================");
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("=== AppDomain 未处理的异常 ===");
            System.Diagnostics.Debug.WriteLine($"异常类型: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"异常消息: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"堆栈跟踪:\n{ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"内部异常: {ex.InnerException.Message}");
                System.Diagnostics.Debug.WriteLine($"内部异常堆栈:\n{ex.InnerException.StackTrace}");
            }

            System.Diagnostics.Debug.WriteLine("===========================");
        }
    }

    protected override MauiApp CreateMauiApp()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== CreateMauiApp 开始 ===");
            var app = MauiProgram.CreateMauiApp();
            System.Diagnostics.Debug.WriteLine("=== CreateMauiApp 完成 ===");
            return app;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== CreateMauiApp 异常 ===");
            System.Diagnostics.Debug.WriteLine($"异常类型: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"异常消息: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"堆栈跟踪:\n{ex.StackTrace}");
            throw;
        }
    }
}
