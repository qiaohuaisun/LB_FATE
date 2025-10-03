using Plugin.LocalNotification;

namespace LB_FATE.Mobile.Services;

/// <summary>
/// 通知服务 - 处理本地推送通知
/// </summary>
public class NotificationService
{
    private bool _notificationsEnabled = true;
    private const int TurnNotificationId = 1001;
    private bool _channelCreated = false;

    /// <summary>
    /// 启用或禁用通知
    /// </summary>
    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set => _notificationsEnabled = value;
    }

    /// <summary>
    /// 请求通知权限
    /// </summary>
    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            // 确保Android通知通道已创建
            EnsureNotificationChannel();

            var result = await LocalNotificationCenter.Current.AreNotificationsEnabled();
            if (!result)
            {
                await LocalNotificationCenter.Current.RequestNotificationPermission();
            }
            return await LocalNotificationCenter.Current.AreNotificationsEnabled();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"请求通知权限失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 确保Android通知通道已创建（仅Android 8.0+需要）
    /// </summary>
    private void EnsureNotificationChannel()
    {
        if (_channelCreated)
            return;

        try
        {
#if ANDROID
#pragma warning disable CA1416 // 平台兼容性已通过版本检查确保
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                // 创建通知通道
                var channel = new Android.App.NotificationChannel(
                    "lbfate_game",
                    "游戏通知",
                    Android.App.NotificationImportance.High)
                {
                    Description = "LB_FATE游戏回合提醒"
                };

                var notificationManager = Android.App.NotificationManager.FromContext(Android.App.Application.Context);
                notificationManager?.CreateNotificationChannel(channel);

                _channelCreated = true;
            }
#pragma warning restore CA1416
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"创建通知通道失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送回合通知
    /// </summary>
    public async Task SendTurnNotificationAsync()
    {
        if (!_notificationsEnabled)
            return;

        try
        {
            var notification = new NotificationRequest
            {
                NotificationId = TurnNotificationId,
                Title = "轮到你了！",
                Description = "现在是你的回合，快来操作吧！",
                BadgeNumber = 1,
                CategoryType = NotificationCategoryType.Status,
                Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                {
                    Priority = Plugin.LocalNotification.AndroidOption.AndroidPriority.High,
                    ChannelId = "lbfate_game"
                },
                iOS = new Plugin.LocalNotification.iOSOption.iOSOptions
                {
                    HideForegroundAlert = false
                }
            };

            await LocalNotificationCenter.Current.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"发送通知失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清除所有通知
    /// </summary>
    public void ClearAllNotifications()
    {
        try
        {
            LocalNotificationCenter.Current.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"清除通知失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清除特定通知
    /// </summary>
    public void ClearTurnNotification()
    {
        try
        {
            LocalNotificationCenter.Current.Cancel(TurnNotificationId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"清除通知失败: {ex.Message}");
        }
    }
}
