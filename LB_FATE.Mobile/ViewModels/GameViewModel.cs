using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LB_FATE.Mobile.Models;
using LB_FATE.Mobile.Services;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace LB_FATE.Mobile.ViewModels;

/// <summary>
/// 游戏页面 ViewModel - 负责游戏交互和状态显示
/// </summary>
public partial class GameViewModel : ObservableObject, IQueryAttributable
{
    private NetworkService? _networkService;
    private readonly GameProtocolHandler _protocolHandler;
    private bool _collectingSkillList = false;
    private bool _collectingHelpText = false;
    private readonly System.Text.StringBuilder _helpTextBuffer = new();
    private bool _collectingInfoText = false;
    private readonly System.Text.StringBuilder _infoTextBuffer = new();
    private string? _lastCommand = null;
    private WeakReference<Views.GamePage>? _gamePageRef = null;
    private readonly DialogService _dialogService;

    [ObservableProperty]
    private GameState _gameState = new();

    [ObservableProperty]
    private string _commandInput = "";
    partial void OnCommandInputChanged(string value)
    {
        UpdateCommandSuggestions();
    }

    [ObservableProperty]
    private bool _canSendCommand;

    [ObservableProperty]
    private bool _isWaitingForInput;

    [ObservableProperty]
    private GridData _gridData = new();

    [ObservableProperty]
    private InteractionState _interactionState = new();

    [ObservableProperty]
    private ObservableCollection<Models.LogMessage> _logMessages = new();

    private const int MaxLogLines = 100;
    private static readonly bool VerboseLog =
        (Environment.GetEnvironmentVariable("ETBBS_MOBILE_VERBOSE_LOG") ?? string.Empty) is string v &&
        (v.Equals("1", StringComparison.OrdinalIgnoreCase) || v.Equals("true", StringComparison.OrdinalIgnoreCase));


    [ObservableProperty]
    private string _statusTip = "";

    [ObservableProperty]
    private string _playerStatus = "";

    [ObservableProperty]
    private double _playerHpPercent = 1.0;

    [ObservableProperty]
    private double _playerMpPercent = 1.0;

    [ObservableProperty]
    private Color _playerHpColor = Colors.Green;

    [ObservableProperty]
    private string _bossStatus = "";

    [ObservableProperty]
    private double _bossHpPercent = 1.0;

    [ObservableProperty]
    private Color _bossHpColor = Colors.Green;

    [ObservableProperty]
    private bool _isBossMode = false;

    [ObservableProperty]
    private string _dayPhaseDisplay = "Day 1 | Phase 1";

    [ObservableProperty]
    private ObservableCollection<Services.SkillInfo> _skills = new();

    [ObservableProperty]
    private bool _showTelegraphWarning = false;

    [ObservableProperty]
    private string _telegraphWarningText = "";

    // Command auto-complete
    [ObservableProperty]
    private ObservableCollection<string> _commandSuggestions = new();

    [ObservableProperty]
    private bool _showCommandSuggestions;

    private void UpdateCommandSuggestions()
    {
        try
        {
            // Build autocomplete context
            var units = GridData?.Units?.Select(u => new UnitBrief(
                Id: u.Id,
                X: u.X,
                Y: u.Y,
                IsAlly: u.IsAlly,
                IsAlive: u.HP > 0,
                IsOffline: u.IsOffline
            )).ToList() ?? new List<UnitBrief>();

            // Resolve player position (fallback to first ally or (0,0))
            var me = GridData?.Units?.FirstOrDefault(u => u.IsCurrentPlayer) ?? GridData?.Units?.FirstOrDefault(u => u.IsAlly);
            int px = me?.X ?? 0, py = me?.Y ?? 0;

            var skills = Skills?.ToList() ?? new List<Services.SkillInfo>();
            var ctx = new AutoCompleteContext(px, py, units, skills);

            var list = CommandAutoComplete.GetCompletions(CommandInput ?? string.Empty, ctx);
            CommandSuggestions.Clear();
            foreach (var s in list.Take(12)) CommandSuggestions.Add(s);
            ShowCommandSuggestions = CommandSuggestions.Count > 0;
        }
        catch
        {
            try { ShowCommandSuggestions = false; CommandSuggestions.Clear(); } catch { }
        }
    }

    [RelayCommand]
    private void AcceptSuggestion(string suggestion)
    {
        suggestion ??= string.Empty;
        var input = CommandInput ?? string.Empty;
        string newInput;
        if (string.IsNullOrWhiteSpace(input))
        {
            newInput = suggestion + " ";
        }
        else
        {
            // Replace last token when there is no trailing space; otherwise append
            if (!input.EndsWith(' '))
            {
                int lastSpace = input.LastIndexOf(' ');
                if (lastSpace < 0) newInput = suggestion + " ";
                else newInput = input.Substring(0, lastSpace + 1) + suggestion + " ";
            }
            else
            {
                newInput = input + suggestion + " ";
            }
        }
        CommandInput = newInput;
        ShowCommandSuggestions = false;
        CommandSuggestions.Clear();
    }


    public GameViewModel(DialogService dialogService)
    {
        _dialogService = dialogService;
        _protocolHandler = new GameProtocolHandler();
        _protocolHandler.GameMessage += OnGameMessage;
        _protocolHandler.NeedInput += OnNeedInput;
        _protocolHandler.TurnStarted += OnTurnStarted;
        _protocolHandler.GameEnded += OnGameEnded;
        _protocolHandler.GameStateUpdated += OnGameStateUpdated;
        _protocolHandler.SkillsUpdated += OnSkillsUpdated;
        _protocolHandler.TelegraphWarning += OnTelegraphWarning;
        _protocolHandler.BossQuote += OnBossQuote;

        // 设置初始状态文本
        PlayerStatus = "等待游戏数据...";
        BossStatus = "";
    }

    /// <summary>
    /// 设置GamePage引用（用于调用动画方法）
    /// </summary>
    public void SetGamePage(Views.GamePage gamePage)
    {
        _gamePageRef = new WeakReference<Views.GamePage>(gamePage);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("NetworkService", out var service) && service is NetworkService netService)
        {
            _networkService = netService;
            _networkService.MessageReceived += OnMessageReceived;
            _networkService.PromptReceived += OnPromptReceived;
            _networkService.Disconnected += OnDisconnected;

            GameState.IsConnected = true;
            AddGameMessage("已连接到游戏服务器，等待游戏开始...");

            InitializeGrid();

            CanSendCommand = true;
            UpdateCommandSuggestions();
        }
    }

    /// <summary>
    /// 初始化网格 - 等待服务器数据
    /// </summary>
    private void InitializeGrid()
    {
        GridData = new GridData
        {
            Width = GameState.GridWidth,
            Height = GameState.GridHeight
        };

        AppendToLog("等待服务器发送游戏地图数据...");
    }


    [RelayCommand]
    private async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CommandInput) || _networkService == null)
            return;

        string cmd = CommandInput.Trim();
        AddGameMessage($"> {cmd}");

        var cmdLower = cmd.ToLowerInvariant();
        if (cmdLower.StartsWith("info") || cmdLower.StartsWith("i ") || cmdLower == "i")
            _lastCommand = "info";
        else if (cmdLower.StartsWith("skills") || cmdLower.StartsWith("s ") || cmdLower == "s")
            _lastCommand = "skills";
        else if (cmdLower.StartsWith("help") || cmdLower.StartsWith("h ") || cmdLower == "h")
            _lastCommand = "help";
        else
            _lastCommand = null;

        bool sent = await _networkService.SendCommandAsync(cmd);
        if (sent)
        {
            CommandInput = "";
            IsWaitingForInput = false;
            CanSendCommand = false;
            try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
        }
        else
        {
            AddGameMessage("发送命令失败");
        }
    }

    [RelayCommand]
    private void QuickCommand(string command)
    {
        CommandInput = command;
        _ = SendCommandAsync();
    }


    private void OnMessageReceived(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _protocolHandler.HandleMessage(message);
        });
    }

    private void OnPromptReceived(string prompt)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _protocolHandler.HandlePrompt();

            // Finalize help/info collection when PROMPT arrives via event (some transports do not deliver PROMPT as a normal line)
            if (_collectingHelpText)
            {
                _collectingHelpText = false;
                var helpText = _helpTextBuffer.ToString().TrimEnd();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    await _dialogService.ShowLongTextAsync("❓ 命令帮助", helpText, "关闭");
                });
            }

            if (_collectingInfoText)
            {
                _collectingInfoText = false;
                var infoText = _infoTextBuffer.ToString().TrimEnd();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    await _dialogService.ShowLongTextAsync("📋 角色信息", infoText, "关闭");
                });
            }
        });
    }

    private void OnGameMessage(string message)
    {
        var clean = StripAnsi(message).Trim();
        if (string.IsNullOrWhiteSpace(clean)) return;

        // 先尝试交互式处理（skills/help/info 收集并弹窗）
        if (TryHandleInteractiveMessage(clean)) return;

        // 再过滤掉纯渲染/装饰性行（不影响逻辑的框线/坐标/图例等）
        if (ShouldFilterMessage(clean)) return;

        // 其它按普通日志处理
        AppendToLog(clean);
    }

    private void OnNeedInput()
    {
        IsWaitingForInput = true;
        CanSendCommand = true;
        // 不再自动聚焦输入框弹出软键盘，因为有智能补全和技能栏可以点击使用
        // try
        // {
        //     if (_gamePageRef != null && _gamePageRef.TryGetTarget(out var page))
        //     {
        //         MainThread.BeginInvokeOnMainThread(() => page.FocusCommandEntry());
        //     }
        // }
        // catch { }
    }

    private void OnTurnStarted()
    {
        GameState.IsMyTurn = true;
        // 保留 _lastCommand，直到交互式收集开始时由收集器重置，
        // 以避免在 PROMPT 到达时过早清空导致 help/info 弹窗无法触发。
    }

    private void OnGameEnded()
    {
        GameState.IsGameActive = false;
        GameState.IsMyTurn = false;
        CanSendCommand = false;
        AppendToLog("========== 游戏结束 ==========");
        AppendToLog("对局已结束");
    }

    private void OnSkillsUpdated(List<Services.SkillInfo> skillList)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Skills.Clear();
            foreach (var skill in skillList)
            {
                Skills.Add(skill);
            }
        });
    }

    private void OnTelegraphWarning(string warningText)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TelegraphWarningText = warningText;
            ShowTelegraphWarning = true;

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ShowTelegraphWarning = false;
                });
            });
        });
    }

    private void OnBossQuote(Services.BossQuoteInfo quoteInfo)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Prefer overlay for boss quotes; if overlay is shown, skip logging
            if (_gamePageRef != null && _gamePageRef.TryGetTarget(out var gp))
            {
                _ = gp.ShowBossQuoteOptimizedAsync(quoteInfo.Quote.Trim(), quoteInfo.EventType);
                return;
            }
            // 台词已在协议层去重，直接使用
            var cleanQuote = quoteInfo.Quote.Trim();

            // 根据事件类型添加不同的视觉效果标记
            var emoji = quoteInfo.EventType switch
            {
                "turn_start" => "⚔️",
                "turn_end" => "⚔️",
                "skill" => "✨",
                "hp_threshold" => "🔥",
                _ => "💬"
            };

            // 为Boss台词添加特殊的颜色标记
            var logMessage = Models.LogMessage.Create($"{emoji} 【BOSS】：\"{cleanQuote}\"");
            // Boss台词使用鲜红色高亮
            logMessage.TextColor = Color.FromArgb("#CF222E");
            logMessage.IsBold = true;

            LogMessages.Add(logMessage);

            // 限制日志条目数量
            while (LogMessages.Count > 100)
            {
                LogMessages.RemoveAt(0);
            }

            // 触发打字机动画效果 - 使用清理后的台词
            if (_gamePageRef != null && _gamePageRef.TryGetTarget(out var gamePage))
            {
                _ = gamePage.ShowBossQuoteOptimizedAsync(cleanQuote, quoteInfo.EventType);
            }
        });
    }

    private void OnGameStateUpdated(Services.GameStateInfo stateInfo)
    {
        // 在收集 help/info/skills 弹窗期间，忽略地图更新，避免清空单位
        if (_collectingHelpText || _collectingInfoText || _collectingSkillList)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            DayPhaseDisplay = $"Day {stateInfo.Day} | Phase {stateInfo.Phase}";

            var newGridData = new GridData
            {
                Width = stateInfo.GridWidth,
                Height = stateInfo.GridHeight
            };

            Services.UnitInfo? playerUnit = null;
            Services.UnitInfo? bossUnit = null;

            // 调试日志：显示收到的单位数量
            System.Diagnostics.Debug.WriteLine($"[GameViewModel] 收到 {stateInfo.Units.Count} 个单位");

            foreach (var unitInfo in stateInfo.Units)
            {
                // 调试日志：显示每个单位详细信息
                System.Diagnostics.Debug.WriteLine($"[GameViewModel] 单位: Id={unitInfo.Id}, Symbol={unitInfo.Symbol}, HP={unitInfo.HP}, IsAlly={unitInfo.IsAlly}, Pos=({unitInfo.X},{unitInfo.Y})");

                // Skip dead units (HP <= 0)
                if (unitInfo.HP <= 0)
                {
                    // Check if it's the player who died
                    if (unitInfo.IsAlly && unitInfo.Symbol == "1")
                    {
                        AppendToLog($"💀 你已阵亡！");
                        GameState.IsGameActive = false;
                        GameState.IsMyTurn = false;
                        CanSendCommand = false;
                    }
                    else
                    {
                        AppendToLog($"💀 {unitInfo.Id} 已阵亡");
                    }
                    continue;
                }

                var gridUnit = ConvertToGridUnit(unitInfo);
                newGridData.AddOrUpdateUnit(gridUnit);
                System.Diagnostics.Debug.WriteLine($"[GameViewModel] 添加单位到网格: {gridUnit.Id} at ({gridUnit.X},{gridUnit.Y})");

                // 从图例解析的单位中，IsAlly=true 且 非Boss = 当前玩家
                if (unitInfo.IsAlly && unitInfo.Symbol != "B")
                {
                    playerUnit = unitInfo;
                    System.Diagnostics.Debug.WriteLine($"[GameViewModel] 找到当前玩家: {unitInfo.Id}, Symbol={unitInfo.Symbol}");
                }

                if (unitInfo.Symbol == "B")
                {
                    bossUnit = unitInfo;
                }
            }

            GridData = newGridData;

            // 只有在高亮单元格发生变化时才更新，减少不必要的 UI 刷新
            if (stateInfo.HighlightedCells.Count > 0)
            {
                bool hasChanges = InteractionState.HighlightedCells.Count != stateInfo.HighlightedCells.Count ||
                                  !InteractionState.HighlightedCells.SequenceEqual(stateInfo.HighlightedCells);

                if (hasChanges)
                {
                    InteractionState.HighlightedCells.Clear();
                    foreach (var cell in stateInfo.HighlightedCells)
                    {
                        InteractionState.HighlightedCells.Add(cell);
                    }
                    InteractionState.Mode = InteractionMode.Move;
                    OnPropertyChanged(nameof(InteractionState));
                    if (VerboseLog) AppendToLog($"💡 显示可移动范围 - {stateInfo.HighlightedCells.Count}个格子");
                }
            }

            // 调试日志：检查是否找到玩家和Boss
            System.Diagnostics.Debug.WriteLine($"[GameViewModel] playerUnit: {(playerUnit != null ? playerUnit.Id : "null")}, bossUnit: {(bossUnit != null ? bossUnit.Id : "null")}");

            if (playerUnit != null)
            {
                UpdatePlayerStatus(playerUnit);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[GameViewModel] 警告：未找到玩家单位！");
                PlayerStatus = "⚠️ 未找到玩家信息";
            }

            if (bossUnit != null)
            {
                IsBossMode = true;
                UpdateBossStatus(bossUnit);
            }
            else
            {
                IsBossMode = false;
                BossStatus = "";
            }

            if (VerboseLog) AppendToLog($"✅ 地图已更新 - {stateInfo.GridWidth}x{stateInfo.GridHeight}, {stateInfo.Units.Count}个单位");
        });
    }

    private GridUnit ConvertToGridUnit(Services.UnitInfo unitInfo)
    {
        Color color;
        bool isCurrentPlayer = unitInfo.IsAlly;  // 图例中IsAlly=true的就是当前玩家

        if (unitInfo.IsAlly)
        {
            color = Colors.Green;  // 当前玩家用绿色
        }
        else if (unitInfo.Symbol == "B")
        {
            color = Colors.Red;  // Boss用红色
        }
        else
        {
            color = Colors.Blue;  // 其他玩家用蓝色
        }

        return new GridUnit
        {
            Id = unitInfo.Id,
            Name = unitInfo.Name,
            Symbol = unitInfo.Symbol,  // 设置网格显示符号
            X = unitInfo.X,
            Y = unitInfo.Y,
            HP = unitInfo.HP,
            MaxHP = unitInfo.MaxHP,
            MP = unitInfo.MP,
            MaxMP = unitInfo.MaxMP,
            IsAlly = unitInfo.IsAlly,
            IsCurrentPlayer = isCurrentPlayer,
            Color = color,
            Tags = new List<string>(unitInfo.Tags),
            IsOffline = unitInfo.IsOffline
        };
    }

    /// <summary>
    /// 更新玩家状态显示
    /// </summary>
    private void UpdatePlayerStatus(Services.UnitInfo unitInfo)
    {
        var hpPercent = unitInfo.MaxHP > 0 ? (int)((double)unitInfo.HP / unitInfo.MaxHP * 100) : 0;
        var className = string.IsNullOrEmpty(unitInfo.ClassName) ? "" : $"[{unitInfo.ClassName}] ";

        // 调试日志
        System.Diagnostics.Debug.WriteLine($"[UpdatePlayerStatus] Id={unitInfo.Id}, ClassName={unitInfo.ClassName}, HP={unitInfo.HP}/{unitInfo.MaxHP}, MP={unitInfo.MP}/{unitInfo.MaxMP}");

        PlayerStatus = $"⭐ {className}❤️ {unitInfo.HP}/{unitInfo.MaxHP} ({hpPercent}%)  ⚡ {unitInfo.MP}/{unitInfo.MaxMP}  📍 ({unitInfo.X},{unitInfo.Y})";

        PlayerHpPercent = unitInfo.MaxHP > 0 ? (double)unitInfo.HP / unitInfo.MaxHP : 0;
        PlayerMpPercent = unitInfo.MaxMP > 0 ? (double)unitInfo.MP / unitInfo.MaxMP : 0;

        PlayerHpColor = PlayerHpPercent > 0.7 ? Color.FromArgb("#1A7F37") :
                       PlayerHpPercent > 0.3 ? Color.FromArgb("#BF8700") : Color.FromArgb("#CF222E");
    }

    /// <summary>
    /// 更新Boss状态显示
    /// </summary>
    private void UpdateBossStatus(Services.UnitInfo unitInfo)
    {
        var hpPercent = unitInfo.MaxHP > 0 ? (int)((double)unitInfo.HP / unitInfo.MaxHP * 100) : 0;
        var className = string.IsNullOrEmpty(unitInfo.ClassName) ? "" : $"[{unitInfo.ClassName}] ";

        // 调试日志
        System.Diagnostics.Debug.WriteLine($"[UpdateBossStatus] Id={unitInfo.Id}, Name={unitInfo.Name}, ClassName={unitInfo.ClassName}, HP={unitInfo.HP}/{unitInfo.MaxHP}, MP={unitInfo.MP}/{unitInfo.MaxMP}");

        BossStatus = $"💀 {className}{unitInfo.Name}  ❤️ {unitInfo.HP}/{unitInfo.MaxHP} ({hpPercent}%)  ⚡ {unitInfo.MP}/{unitInfo.MaxMP}  📍 ({unitInfo.X},{unitInfo.Y})";

        BossHpPercent = unitInfo.MaxHP > 0 ? (double)unitInfo.HP / unitInfo.MaxHP : 0;
        BossHpColor = BossHpPercent > 0.7 ? Color.FromArgb("#1A7F37") :
                     BossHpPercent > 0.3 ? Color.FromArgb("#BF8700") : Color.FromArgb("#CF222E");
    }

    private void OnDisconnected(string reason)
    {
        GameState.IsConnected = false;
        AppendToLog("========== 连接断开 ==========");
        AppendToLog(reason);
    }

    private static readonly Regex AnsiRegex = new("\x1B\\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex SkillItemPattern1 = new("^\\s*(\\d+)\\s*[:\\.\\)]\\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex SkillItemPattern2 = new("^\\s*\\[(\\d+)\\]\\s*(.+)$", RegexOptions.Compiled);

    private CancellationTokenSource? _tipCts;

    private void AddGameMessage(string message)
    {
        var clean = StripAnsi(message).Trim();
        if (string.IsNullOrEmpty(clean)) return;

        // 过滤掉ASCII棋盘和冗余状态信息
        if (ShouldFilterMessage(clean))
        {
            return;
        }

        // 优先尝试交互式处理（技能列表、info、help等）
        if (TryHandleInteractiveMessage(clean))
        {
            return;
        }

        // 添加到日志
        AppendToLog(clean);
    }


    // 基于规则的过滤：减少 UI 日志冗余
    private static readonly Regex BoardRowRegex = new("\\|.*\\|\\s*\\d+$|^\\|.*\\|$", RegexOptions.Compiled);
    private static readonly Regex BorderRegex = new("^\\+[-]+\\+\\s*$", RegexOptions.Compiled);
    private static readonly Regex DigitsLineRegex = new("^\\s*[0-9 ]+\\s*$", RegexOptions.Compiled);
    private static readonly Regex LegendUnitRegex = new("^\\s*[!B1-9]:\\s+", RegexOptions.Compiled);

    private static bool ShouldFilterMessage(string s)
    {
        // 棋盘标题/边框/坐标轴/网格行
        if (s.StartsWith("=== LB_FATE") || BorderRegex.IsMatch(s) || DigitsLineRegex.IsMatch(s) || BoardRowRegex.IsMatch(s))
            return true;

        // 图例/状态区 & 最近事件标题/列表
        if (s.Contains("图例") || s.Contains("状态：") || s.Equals("最近事件") || s.StartsWith(" - "))
            return true;

        // 指令帮助区域
        if (s.StartsWith("命令：") || s.StartsWith("消耗") || s.StartsWith("注意") || s.StartsWith("提示"))
            return true;

        // 图例里的单位行
        if (LegendUnitRegex.IsMatch(s))
            return true;

        // 服务器内部日志
        if (s.StartsWith("[Srv] "))
            return true;

        // 初始化消息
        if (s.StartsWith("- [初始化]") || s.Contains("已加载角色") || s.Contains("已创建单位"))
            return true;

        // 最近记录标题
        if (s.StartsWith("最近记录") || s.Equals("最近记录："))
            return true;

        // 回合边框装饰
        if (s.Contains("╔═") || s.Contains("╚═") || s.Contains("║"))
            return true;

        // 其他保留：错误/警告/系统提示/胜负/连接信息等默认保留
        return false;
    }

    private readonly object _logLock = new object();
    private readonly List<string> _logQueue = new List<string>();
    private bool _isProcessingLog = false;
    private readonly Dictionary<string, DateTime> _recentLogTimestamps = new();
    private readonly TimeSpan _logDedupInterval = TimeSpan.FromSeconds(2);

    private void AppendToLog(string message)
    {
        lock (_logLock)
        {
            var now = DateTime.UtcNow;
            if (_recentLogTimestamps.TryGetValue(message, out var lastTs))
            {
                if ((now - lastTs) < _logDedupInterval)
                {
                    // drop duplicate within dedup window
                    return;
                }
            }
            _recentLogTimestamps[message] = now;

            // prune old entries
            if (_recentLogTimestamps.Count > 256)
            {
                var cutoff = now - TimeSpan.FromSeconds(30);
                var keys = _recentLogTimestamps.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
                foreach (var k in keys) _recentLogTimestamps.Remove(k);
            }

            _logQueue.Add(message);

            if (_isProcessingLog)
                return;

            _isProcessingLog = true;
        }

        // 批量处理日志，减少 UI 刷新次数
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(50); // 50ms 批处理延迟

            List<string> messagesToProcess;
            lock (_logLock)
            {
                messagesToProcess = new List<string>(_logQueue);
                _logQueue.Clear();
                _isProcessingLog = false;
            }

            foreach (var msg in messagesToProcess)
            {
                // clear previous logs and show only this message
                LogMessages.Clear();
                var logMsg = Models.LogMessage.Create(msg);
                LogMessages.Add(logMsg);
            }
            });
    }

    private bool TryHandleInteractiveMessage(string text)
    {
        // 技能列表开始检测 - 检测技能条目格式 "[数字] 技能名 (mp:..."
        // 技能信息会自动显示在UI技能面板中，这里直接过滤掉日志中的技能条目
        if (!_collectingSkillList && !_collectingHelpText && !_collectingInfoText)
        {
            var skillMatch = SkillItemPattern2.Match(text);
            if (skillMatch.Success && text.Contains("mp:") && text.Contains("cd:"))
            {
                // 这是技能列表的第一个条目（过滤掉，技能会显示在UI技能面板）
                return true;
            }
        }

        // help: trigger collection when last command is 'help'
        if (!_collectingHelpText && !_collectingInfoText && !_collectingSkillList &&
            _lastCommand == "help" &&
            !text.Equals("PROMPT", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(text))
        {
            _collectingHelpText = true;
            _helpTextBuffer.Clear();
            _helpTextBuffer.AppendLine(text);
            _lastCommand = null;
            return true;
        }

        // help命令开始检测
        if (!_collectingHelpText && !_collectingInfoText && !_collectingSkillList &&
            ((text.Contains("move") && text.Contains("移动")) ||
             (text.Contains("attack") && text.Contains("攻击")) ||
             (text.StartsWith("move x y") || text.StartsWith("attack P#"))))
        {
            _collectingHelpText = true;
            _helpTextBuffer.Clear();
            _helpTextBuffer.AppendLine(text);
            return true;
        }

        // 收集help命令内容
        if (_collectingHelpText)
        {
            // 检测结束条件：空行、PROMPT、或不符合命令格式的行
            if (string.IsNullOrWhiteSpace(text) ||
                text.Equals("PROMPT", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("Commands:", StringComparison.OrdinalIgnoreCase))
            {
                _collectingHelpText = false;
                var helpText = _helpTextBuffer.ToString().TrimEnd();

                // 显示弹窗
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // 短暂延迟确保UI稳定
                    await _dialogService.ShowLongTextAsync("❓ 命令帮助", helpText, "关闭");
                });
                return true;
            }

            // 继续收集help行
            if (text.Contains(":") || text.Contains("|") || text.Contains("注意") || text.Contains("提示") || text.Contains("消耗"))
            {
                _helpTextBuffer.AppendLine(text);
                return true;
            }
            else
            {
                // 不符合help格式，结束收集
                _collectingHelpText = false;
                var helpText = _helpTextBuffer.ToString().TrimEnd();

                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await _dialogService.ShowLongTextAsync("❓ 命令帮助", helpText, "关闭");
                });
                return false;
            }
        }

        // info命令开始检测 - 基于_lastCommand标志
        if (!_collectingInfoText && !_collectingHelpText && !_collectingSkillList &&
            _lastCommand == "info" &&
            !text.Equals("PROMPT", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(text))
        {
            // info命令的响应开始
            _collectingInfoText = true;
            _infoTextBuffer.Clear();
            _infoTextBuffer.AppendLine(text);
            _lastCommand = null; // 清除标志
            return true;
        }

        // 收集info命令内容
        if (_collectingInfoText)
        {
            if (string.IsNullOrWhiteSpace(text) ||
                text.Equals("PROMPT", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("Commands:", StringComparison.OrdinalIgnoreCase))
            {
                _collectingInfoText = false;
                var infoText = _infoTextBuffer.ToString().TrimEnd();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await _dialogService.ShowLongTextAsync("📋 角色信息", infoText, "关闭");
                });
                return true;
            }

            // 若遇到棋盘/图例/命令提示等非 info 内容，提前结束并弹窗
                        if (BorderRegex.IsMatch(text) || BoardRowRegex.IsMatch(text) || DigitsLineRegex.IsMatch(text)
                || text.Contains("Legend", StringComparison.OrdinalIgnoreCase)
                || text.Contains("图例") || text.Contains("状态")
                || text.StartsWith("=== LB_FATE")
                || text.StartsWith("Commands:", StringComparison.OrdinalIgnoreCase))
            {
                _collectingInfoText = false;
                var infoText2 = _infoTextBuffer.ToString().TrimEnd();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await _dialogService.ShowLongTextAsync("📋 角色信息", infoText2, "关闭");
                });
                return true;
            }

            _infoTextBuffer.AppendLine(text);
            return true;
        }



        return false;
    }

    private static string StripAnsi(string text)
    {
        try { return AnsiRegex.Replace(text, string.Empty); } catch { return text; }
    }

    private bool IsImportantMessage(string text)
    {
        if (text.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("错误") ||
            text.Contains("失败") ||
            text.Contains("游戏结束") ||
            text.StartsWith("连接断开") ||
            text.StartsWith("断开连接"))
        {
            return true;
        }

        return false;
    }

    private void ShowTip(string text, int milliseconds = 3000)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusTip = text.Length > 140 ? text.Substring(0, 140) + "…" : text;

            _tipCts?.Cancel();
            var cts = new CancellationTokenSource();
            _tipCts = cts;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(milliseconds, cts.Token);
                    if (!cts.IsCancellationRequested)
                    {
                        MainThread.BeginInvokeOnMainThread(() => StatusTip = string.Empty);
                    }
                }
                catch { }
            });
        });
    }

}



















