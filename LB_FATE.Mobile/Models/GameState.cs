using CommunityToolkit.Mvvm.ComponentModel;

namespace LB_FATE.Mobile.Models;

/// <summary>
/// 游戏状态数据模型
/// </summary>
public partial class GameState : ObservableObject
{
    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isGameActive;

    [ObservableProperty]
    private bool _isMyTurn;

    [ObservableProperty]
    private string? _playerId;

    [ObservableProperty]
    private string? _roleName;

    [ObservableProperty]
    private int _currentHP;

    [ObservableProperty]
    private int _maxHP;

    [ObservableProperty]
    private float _currentMP;

    [ObservableProperty]
    private float _maxMP;

    [ObservableProperty]
    private int _gridWidth = 25;

    [ObservableProperty]
    private int _gridHeight = 15;

    [ObservableProperty]
    private GridData _grid = new();

    // 已移除：不再保留客户端日志
}

/// <summary>
/// 服务器配置
/// </summary>
public class ServerConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 35500;
}
