using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LB_FATE.Mobile.Services;
using LB_FATE.Mobile.Views;
using System.Net.Sockets;

namespace LB_FATE.Mobile.ViewModels;

/// <summary>
/// 主页面 ViewModel - 负责服务器连接
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly NetworkService _networkService;

    [ObservableProperty]
    private string _serverHost = "127.0.0.1";

    [ObservableProperty]
    private int _serverPort = 35500;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "请输入服务器地址";

    public MainViewModel()
    {
        _networkService = new NetworkService();
        _networkService.Connected += OnConnected;
        _networkService.Disconnected += OnDisconnected;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsConnecting)
            return;

        // 验证输入
        if (string.IsNullOrWhiteSpace(ServerHost))
        {
            StatusMessage = "⚠️ 请输入服务器地址";
            return;
        }

        if (ServerPort < 1 || ServerPort > 65535)
        {
            StatusMessage = "⚠️ 端口号必须在 1-65535 之间";
            return;
        }

        IsConnecting = true;
        StatusMessage = "🔄 正在连接到服务器...";

        try
        {
            bool success = await _networkService.ConnectAsync(ServerHost, ServerPort);

            if (!success)
            {
                StatusMessage = "❌ 连接失败，请检查：\n• 服务器是否已启动\n• 地址和端口是否正确\n• 网络连接是否正常";
                IsConnecting = false;
            }
            // 连接成功后 OnConnected 会被调用
        }
        catch (SocketException ex)
        {
            StatusMessage = $"❌ 网络错误：{GetFriendlyErrorMessage(ex)}";
            IsConnecting = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 连接错误: {ex.Message}";
            IsConnecting = false;
        }
    }

    private string GetFriendlyErrorMessage(SocketException ex)
    {
        return ex.SocketErrorCode switch
        {
            System.Net.Sockets.SocketError.ConnectionRefused => "服务器拒绝连接，请确认服务器已启动",
            System.Net.Sockets.SocketError.HostNotFound => "找不到服务器，请检查地址是否正确",
            System.Net.Sockets.SocketError.TimedOut => "连接超时，请检查网络或服务器状态",
            System.Net.Sockets.SocketError.NetworkUnreachable => "网络不可达，请检查网络连接",
            _ => ex.Message
        };
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _networkService.DisconnectAsync();
    }

    private void OnConnected()
    {
        IsConnected = true;
        IsConnecting = false;
        StatusMessage = "✅ 已连接到服务器，正在进入游戏...";

        // 导航到游戏页面
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(GamePage), new Dictionary<string, object>
                {
                    ["NetworkService"] = _networkService
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ 导航失败: {ex.Message}";
                IsConnected = false;
            }
        });
    }

    private void OnDisconnected(string reason)
    {
        IsConnected = false;
        IsConnecting = false;

        // 提供更友好的断开提示
        StatusMessage = string.IsNullOrWhiteSpace(reason)
            ? "⚠️ 与服务器断开连接"
            : $"⚠️ 已断开连接: {reason}";
    }
}
