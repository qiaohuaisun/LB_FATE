using CommunityToolkit.Maui;
using LB_FATE.Mobile.Services;
using LB_FATE.Mobile.ViewModels;
using LB_FATE.Mobile.Views;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;

namespace LB_FATE.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseLocalNotification()  // 初始化本地通知插件
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Windows平台优化已移至Platforms/Windows/App.xaml.cs

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // 注册服务
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<DialogService>();
        builder.Services.AddSingleton<ToastService>();

        // 注册 ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<GameViewModel>();

        // 注册 Pages
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<GamePage>();

        return builder.Build();
    }
}
