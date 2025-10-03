using LB_FATE.Mobile.ViewModels;

namespace LB_FATE.Mobile;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnLocalhostClicked(object? sender, EventArgs e)
    {
        // 触觉反馈
        try
        {
#if ANDROID || IOS
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
#endif
        }
        catch { }

        // 按钮动画
        if (sender is Button button)
        {
            await AnimateButtonPress(button);
        }

        _viewModel.ServerHost = "127.0.0.1";
    }

    /// <summary>
    /// 按钮按下动画
    /// </summary>
    private async Task AnimateButtonPress(Button button)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await button.ScaleToAsync(0.95, 50, Easing.CubicOut);
            await button.ScaleToAsync(1.0, 50, Easing.CubicIn);
        });
    }
}
