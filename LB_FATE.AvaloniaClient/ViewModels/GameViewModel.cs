using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LB_FATE.AvaloniaClient.Models;
using LB_FATE.AvaloniaClient.Services;
using ETBBS;

namespace LB_FATE.AvaloniaClient.ViewModels;

/// <summary>
/// ViewModel for the main game view.
/// </summary>
public partial class GameViewModel : ViewModelBase
{
    private readonly GameClient _client;
    private readonly GameState _gameState;

    [ObservableProperty]
    private string _serverHost = "127.0.0.1";

    [ObservableProperty]
    private int _serverPort = 35500;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _statusMessage = "Not connected";

    [ObservableProperty]
    private int _day = 1;

    [ObservableProperty]
    private int _phase = 1;

    [ObservableProperty]
    private string _currentPlayer = "";

    [ObservableProperty]
    private bool _isMyTurn;

    [ObservableProperty]
    private string _commandInput = "";

    public ObservableCollection<UnitViewModel> Units { get; } = new();
    public ObservableCollection<string> Logs { get; } = new();
    public ObservableCollection<SkillInfo> Skills { get; } = new();

    public int GridWidth => _gameState.Width;
    public int GridHeight => _gameState.Height;

    public GameViewModel()
    {
        _client = new GameClient();
        _gameState = new GameState();

        _client.MessageReceived += OnMessageReceived;
        _client.ConnectionStatusChanged += OnConnectionStatusChanged;
        _client.ErrorOccurred += OnErrorOccurred;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsConnecting || IsConnected) return;

        IsConnecting = true;
        StatusMessage = "Connecting...";

        var success = await _client.ConnectAsync(ServerHost, ServerPort);

        IsConnecting = false;

        if (success)
        {
            StatusMessage = $"Connected to {ServerHost}:{ServerPort}";
        }
        else
        {
            StatusMessage = "Connection failed";
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _client.Disconnect();
        StatusMessage = "Disconnected";
    }

    [RelayCommand]
    private async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CommandInput)) return;

        await _client.SendCommandAsync(CommandInput);
        CommandInput = "";
    }

    [RelayCommand]
    private async Task MoveAsync(Coord destination)
    {
        await _client.SendCommandAsync($"move {destination.X} {destination.Y}");
    }

    [RelayCommand]
    private async Task AttackAsync(string targetId)
    {
        await _client.SendCommandAsync($"attack {targetId}");
    }

    [RelayCommand]
    private async Task UseSkillAsync(int skillIndex)
    {
        // For now, just send the skill command without target
        // User can type target in command box
        await _client.SendCommandAsync($"use {skillIndex}");
    }

    [RelayCommand]
    private async Task PassAsync()
    {
        await _client.SendCommandAsync("pass");
    }

    [RelayCommand]
    private async Task GetSkillsAsync()
    {
        await _client.SendCommandAsync("skills");
    }

    private void OnMessageReceived(object? sender, string message)
    {
        // Parse the message and update game state
        GameStateParser.ParseLine(_gameState, message);

        // Update observable properties
        Day = _gameState.Day;
        Phase = _gameState.Phase;
        IsMyTurn = _gameState.IsMyTurn;

        // Update units
        UpdateUnits();

        // Update logs
        if (message.StartsWith(" - "))
        {
            Logs.Insert(0, message.Substring(3));
            if (Logs.Count > 50)
                Logs.RemoveAt(Logs.Count - 1);
        }
        else if (!string.IsNullOrWhiteSpace(message) &&
                 !message.StartsWith("===") &&
                 !message.StartsWith("+") &&
                 !message.StartsWith("|") &&
                 !message.StartsWith("Legend") &&
                 !message.StartsWith("Commands") &&
                 !message.StartsWith("Costs") &&
                 !message.StartsWith("Phases") &&
                 !message.StartsWith("Recent") &&
                 message != "PROMPT")
        {
            Logs.Insert(0, message);
            if (Logs.Count > 50)
                Logs.RemoveAt(Logs.Count - 1);
        }

        // Clear turn flag after receiving non-PROMPT message
        if (message != "PROMPT" && IsMyTurn)
        {
            IsMyTurn = false;
        }
    }

    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        IsConnected = isConnected;
        StatusMessage = isConnected ? "Connected" : "Disconnected";
    }

    private void OnErrorOccurred(object? sender, string error)
    {
        StatusMessage = error;
        Logs.Insert(0, $"[ERROR] {error}");
    }

    private void UpdateUnits()
    {
        // Update existing units or add new ones
        foreach (var unitInfo in _gameState.Units.Values)
        {
            var existing = Units.FirstOrDefault(u => u.Id == unitInfo.Id);
            if (existing != null)
            {
                existing.Update(unitInfo);
            }
            else
            {
                Units.Add(new UnitViewModel(unitInfo));
            }
        }

        // Remove dead units (HP <= 0)
        var deadUnits = Units.Where(u => u.Hp <= 0).ToList();
        foreach (var dead in deadUnits)
        {
            Units.Remove(dead);
        }
    }
}

/// <summary>
/// ViewModel for a single unit on the game board.
/// </summary>
public partial class UnitViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = "";

    [ObservableProperty]
    private string _className = "";

    [ObservableProperty]
    private int _hp;

    [ObservableProperty]
    private int _maxHp;

    [ObservableProperty]
    private double _mp;

    [ObservableProperty]
    private Coord _position;

    [ObservableProperty]
    private char _symbol;

    [ObservableProperty]
    private bool _isOffline;

    public double HpPercentage => MaxHp > 0 ? (double)Hp / MaxHp * 100 : 0;

    public UnitViewModel(UnitInfo info)
    {
        Update(info);
    }

    public void Update(UnitInfo info)
    {
        Id = info.Id;
        ClassName = info.ClassName;
        Hp = info.Hp;
        MaxHp = info.MaxHp;
        Mp = info.Mp;
        Position = info.Position;
        Symbol = info.Symbol;
        IsOffline = info.IsOffline;
        OnPropertyChanged(nameof(HpPercentage));
    }
}