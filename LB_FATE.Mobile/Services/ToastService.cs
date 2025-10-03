using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace LB_FATE.Mobile.Services;

public class ToastService
{
    public async Task ShowToastAsync(string message, ToastDuration duration = ToastDuration.Short, double textSize = 14)
    {
        try
        {
            var toast = Toast.Make(message, duration, textSize);
            await toast.Show();
        }
        catch { }
    }

    public async Task ShowSnackbarAsync(string message, string? actionText = null, Action? action = null, TimeSpan? duration = null)
    {
        try
        {
            duration ??= TimeSpan.FromSeconds(3);
            var snackbar = Snackbar.Make(
                message,
                action: action,
                actionButtonText: actionText ?? "确定",
                duration: duration.Value);
            await snackbar.Show();
        }
        catch { }
    }
}


