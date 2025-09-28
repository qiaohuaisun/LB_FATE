using System.Globalization;
using System.Resources;

namespace LB_FATE.MauiClient;

// Lightweight resource accessor for runtime localization without designer codegen.
public static class AppResources
{
    private static readonly ResourceManager Rm = new(
        "LB_FATE.MauiClient.Resources.Strings.AppResources",
        typeof(AppResources).Assembly);

    public static CultureInfo? OverrideCulture { get; set; }

    private static string Get(string name)
    {
        var culture = OverrideCulture ?? CultureInfo.CurrentUICulture;
        return Rm.GetString(name, culture) ?? name;
    }

    public static string AppTitle => Get(nameof(AppTitle));
    public static string HostPlaceholder => Get(nameof(HostPlaceholder));
    public static string PortPlaceholder => Get(nameof(PortPlaceholder));
    public static string Connect => Get(nameof(Connect));
    public static string Disconnect => Get(nameof(Disconnect));
    public static string StatusConnected => Get(nameof(StatusConnected));
    public static string StatusNotConnected => Get(nameof(StatusNotConnected));
    public static string Discover => Get(nameof(Discover));
    public static string DiscoveredHostsTitle => Get(nameof(DiscoveredHostsTitle));
    public static string Use => Get(nameof(Use));
    public static string Send => Get(nameof(Send));
    public static string Settings => Get(nameof(Settings));
    public static string AutoReconnect => Get(nameof(AutoReconnect));
    public static string DiscoverTitle => Get(nameof(DiscoverTitle));
    public static string Refresh => Get(nameof(Refresh));
    public static string UseSelected => Get(nameof(UseSelected));
    public static string NoSelection => Get(nameof(NoSelection));
    public static string CommandPlaceholderWaiting => Get(nameof(CommandPlaceholderWaiting));
    public static string CommandPlaceholderReady => Get(nameof(CommandPlaceholderReady));

    public static string ErrorTitle => Get(nameof(ErrorTitle));
    public static string ErrorHostRequired => Get(nameof(ErrorHostRequired));
    public static string ErrorInvalidPort => Get(nameof(ErrorInvalidPort));
    public static string ConnectFailed => Get(nameof(ConnectFailed));
    public static string SendFailed => Get(nameof(SendFailed));
    public static string NotConnectedTitle => Get(nameof(NotConnectedTitle));
    public static string NotConnectedMessage => Get(nameof(NotConnectedMessage));
    public static string DiscoveryFailed => Get(nameof(DiscoveryFailed));

    public static string ToastConnectFailedAutoRetry => Get(nameof(ToastConnectFailedAutoRetry));
    public static string ToastDisconnectedAutoRetry => Get(nameof(ToastDisconnectedAutoRetry));
    public static string ToastReconnectingInFormat => Get(nameof(ToastReconnectingInFormat));
    public static string ToastReconnected => Get(nameof(ToastReconnected));

    public static string ConnectedToFormat => Get(nameof(ConnectedToFormat));
    public static string DiscoverFoundFormat => Get(nameof(DiscoverFoundFormat));
    public static string DiscoverNone => Get(nameof(DiscoverNone));
    public static string ReconnectAttemptLogFormat => Get(nameof(ReconnectAttemptLogFormat));
    public static string ConnectFailedLogPrefix => Get(nameof(ConnectFailedLogPrefix));

    public static string ReconnectDelaySeconds => Get(nameof(ReconnectDelaySeconds));
    public static string MaxAttempts => Get(nameof(MaxAttempts));
    public static string Language => Get(nameof(Language));
    public static string SystemDefault => Get(nameof(SystemDefault));
    public static string English => Get(nameof(English));
    public static string ChineseSimplified => Get(nameof(ChineseSimplified));
    public static string ResetDefaults => Get(nameof(ResetDefaults));
    public static string AutoReconnectHint => Get(nameof(AutoReconnectHint));
}
