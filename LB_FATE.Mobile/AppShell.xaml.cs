using LB_FATE.Mobile.Views;

namespace LB_FATE.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // 注册导航路由
        Routing.RegisterRoute(nameof(GamePage), typeof(GamePage));
    }
}
