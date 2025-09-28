using Microsoft.Maui.Storage;

namespace LB_FATE.MauiClient;

public static class AppSettings
{
    private const string KeyAutoReconnect = "auto_reconnect";
    private const string KeyReconnectDelaySec = "reconnect_delay_sec";
    private const string KeyReconnectMaxAttempts = "reconnect_max_attempts";
    private const string KeyUiLanguage = "ui_language"; // "system" | "en" | "zh-Hans"
    public static bool AutoReconnect
    {
        get => Preferences.Get(KeyAutoReconnect, true);
        set => Preferences.Set(KeyAutoReconnect, value);
    }

    public static int ReconnectDelaySeconds
    {
        get => Preferences.Get(KeyReconnectDelaySec, 2);
        set => Preferences.Set(KeyReconnectDelaySec, Math.Max(1, value));
    }

    public static int ReconnectMaxAttempts
    {
        get => Preferences.Get(KeyReconnectMaxAttempts, 10);
        set => Preferences.Set(KeyReconnectMaxAttempts, Math.Max(0, value)); // 0 = infinite
    }

    public static string UiLanguage
    {
        get => Preferences.Get(KeyUiLanguage, "system");
        set => Preferences.Set(KeyUiLanguage, value);
    }
}
