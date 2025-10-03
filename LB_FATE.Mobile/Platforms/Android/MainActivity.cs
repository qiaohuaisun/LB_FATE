using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace LB_FATE.Mobile;

/// <summary>
/// Android主Activity - 包含Android特定优化
/// </summary>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    WindowSoftInputMode = SoftInput.AdjustResize)]  // 优化输入法显示
public class MainActivity : MauiAppCompatActivity
{
    private long _lastBackPress;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== MainActivity.OnCreate 开始 ===");

            // 必须先调用 base.OnCreate，Window 才会被完全初始化
            base.OnCreate(savedInstanceState);

            // Android特定的异常处理
            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += OnAndroidUnhandledException;

            // Android性能优化 - 支持刘海屏 (Android 9+)
#pragma warning disable CA1416
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P && Window?.Attributes != null)
            {
                Window.Attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            }
#pragma warning restore CA1416

            // 隐藏状态栏和导航栏，全屏显示
            if (Window != null)
            {
                // 隐藏系统UI（状态栏和导航栏）
                Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);

                // Android 11+ 使用新的隐藏API
#pragma warning disable CA1416, CA1422
                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    Window.SetDecorFitsSystemWindows(false);
                    // 确保 DecorView 已初始化后再获取 InsetsController
                    if (Window.DecorView != null)
                    {
                        Window.InsetsController?.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
                        if (Window.InsetsController != null)
                        {
                            Window.InsetsController.SystemBarsBehavior = (int)Android.Views.WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                        }
                    }
                }
#pragma warning restore CA1416, CA1422
                else
                {
                    // Android 11以下使用旧API
#pragma warning disable CS0618
                    if (Window.DecorView != null)
                    {
                        var uiOptions = (int)Window.DecorView.SystemUiVisibility;
                        uiOptions |= (int)SystemUiFlags.Fullscreen;
                        uiOptions |= (int)SystemUiFlags.HideNavigation;
                        uiOptions |= (int)SystemUiFlags.ImmersiveSticky;
                        Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;
                    }
#pragma warning restore CS0618
                }

                // 保持屏幕常亮（游戏场景）
                Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            }

            System.Diagnostics.Debug.WriteLine("=== MainActivity.OnCreate 完成 ===");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== MainActivity.OnCreate 异常 ===");
            System.Diagnostics.Debug.WriteLine($"异常类型: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"异常消息: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"堆栈跟踪:\n{ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"内部异常: {ex.InnerException.Message}");
                System.Diagnostics.Debug.WriteLine($"内部异常堆栈:\n{ex.InnerException.StackTrace}");
            }

            throw; // 重新抛出以便调试
        }
    }

    /// <summary>
    /// 处理返回键 - 防止误退出
    /// </summary>
#pragma warning disable CS0612 // OnBackPressed is obsolete but still supported for compatibility
    public override void OnBackPressed()
    {
        // 可以在这里添加二次确认对话框
        // 目前直接调用基类处理
                try
        {
            long now = Java.Lang.JavaSystem.CurrentTimeMillis();
            if (now - _lastBackPress < 2000)
            {
                base.OnBackPressed();
            }
            else
            {
                _lastBackPress = now;
                Android.Widget.Toast.MakeText(this, "再次返回退出", Android.Widget.ToastLength.Short)?.Show();
            }
        }
        catch { base.OnBackPressed(); }
    }
#pragma warning restore CS0612

    protected override void OnResume()
    {
        base.OnResume();
        System.Diagnostics.Debug.WriteLine("[MainActivity] App resumed");

        // 恢复全屏模式（防止从其他Activity返回时显示系统栏）
        if (Window != null)
        {
#pragma warning disable CA1416
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                Window.InsetsController?.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
            }
#pragma warning restore CA1416
            else
            {
#pragma warning disable CS0618
                var uiOptions = (int)Window.DecorView.SystemUiVisibility;
                uiOptions |= (int)SystemUiFlags.Fullscreen;
                uiOptions |= (int)SystemUiFlags.HideNavigation;
                uiOptions |= (int)SystemUiFlags.ImmersiveSticky;
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;
#pragma warning restore CS0618
            }
        }
    }

    protected override void OnPause()
    {
        base.OnPause();
        System.Diagnostics.Debug.WriteLine("[MainActivity] App paused");
    }

    private void OnAndroidUnhandledException(object? sender, Android.Runtime.RaiseThrowableEventArgs e)
    {
        var exception = e.Exception;

        System.Diagnostics.Debug.WriteLine("========== Android未处理异常 ==========");
        System.Diagnostics.Debug.WriteLine($"错误: {exception.Message}");
        System.Diagnostics.Debug.WriteLine($"堆栈: {exception.StackTrace}");
        System.Diagnostics.Debug.WriteLine("==========================================");

        // 标记为已处理，防止应用崩溃
        e.Handled = true;
    }

    protected override void OnDestroy()
    {
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser -= OnAndroidUnhandledException;
        base.OnDestroy();
    }
}


