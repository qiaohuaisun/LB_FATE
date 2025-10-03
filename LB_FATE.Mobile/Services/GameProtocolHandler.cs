using System.Text.RegularExpressions;

namespace LB_FATE.Mobile.Services;

/// <summary>
/// 游戏协议处理器，解析服务器消息并触发相应事件
/// </summary>
public class GameProtocolHandler
{
    public event Action<string>? GameMessage;
    public event Action? TurnStarted;
    public event Action? GameEnded;
    public event Action<string>? PlayerInfo;
    public event Action? NeedInput;
    public event Action<GameStateInfo>? GameStateUpdated;
    public event Action<List<SkillInfo>>? SkillsUpdated;
    public event Action<string>? TelegraphWarning;
    public event Action<BossQuoteInfo>? BossQuote;

    private readonly List<string> _messageBuffer = new();
    private readonly Dictionary<string, List<string>> _unitTags = new();
    private readonly Dictionary<string, int> _unitMaxMp = new(); // 追踪每个单位的真实MaxMP
    private string? _lastBossQuote = null;  // 用于在协议层去重（提取后的纯台词）
    private DateTime _lastBossQuoteTime = DateTime.MinValue;  // 最近一次台词时间
    private int _lastDay = 1;
    private int _lastPhase = 1;

    // 图例格式：Symbol: ID ClassName    HP[...](hp/maxhp) MP=mp Pos=(x,y)
    // ID可能包含空格（如"【Beast of Ruin】"），ClassName是单个词
    private static readonly Regex UnitLegendPattern = new(
        @"^\s*([1-9B!]):\s*(.+)\s+(\S+)\s+HP\[.*?\]\((\d+)/(\d+)\)\s+MP=([\d.]+).*?Pos=\((\d+),(\d+)\)",
        RegexOptions.Compiled);

    private static readonly Regex SkillPattern = new(
        @"^\s*\[(\d+)\]\s*(\S+)\s*\(mp:(\d+(?:\.\d+)?),\s*range:(\d+),\s*cd:(\d+)\s*\((\d+)\s*left\)",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> StatusKeywords = new()
    {
        { "眩晕", "stunned" },
        { "stunned", "stunned" },
        { "定身", "rooted" },
        { "rooted", "rooted" },
        { "冰冻", "frozen" },
        { "frozen", "frozen" },
        { "沉默", "silenced" },
        { "silenced", "silenced" },
        { "流血", "bleeding" },
        { "bleeding", "bleeding" },
        { "灼烧", "burning" },
        { "burning", "burning" },
        { "护盾", "shielded" },
        { "shielded", "shielded" },
        { "不死", "undying" },
        { "undying", "undying" }
    };

    /// <summary>
    /// 处理从服务器接收到的消息
    /// </summary>
    public void HandleMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;


        _messageBuffer.Add(message);

        if (_messageBuffer.Count > 100)
        {
            _messageBuffer.RemoveAt(0);
        }

        ParseStatusEffects(message);
        ParseSkills(message);
        CheckTelegraphWarning(message);
        CheckBossQuote(message);

        var gameState = TryParseGameState();
        if (gameState != null)
        {
            GameStateUpdated?.Invoke(gameState);
        }

        if (message.Contains("游戏结束") ||
            message.Contains("胜利") ||
            message.Contains("失败") ||
            message.Contains("==========="))
        {
            GameEnded?.Invoke();
        }

        if (message.StartsWith("你的角色:") || message.Contains("HP:") || message.Contains("MP:"))
        {
            PlayerInfo?.Invoke(message);
        }

        // 过滤掉Boss台词协议消息，避免重复显示
        if (!message.Contains("[BOSS_QUOTE:"))
        {
            GameMessage?.Invoke(message);
        }
    }

    private void ParseStatusEffects(string message)
    {
        var cleanMsg = StripAnsi(message).ToLowerInvariant();

        foreach (var (keyword, tag) in StatusKeywords)
        {
            if (cleanMsg.Contains(keyword.ToLowerInvariant()))
            {
                var unitIdMatch = Regex.Match(message, @"(P\d+|BOSS|B)");
                if (unitIdMatch.Success)
                {
                    string unitId = unitIdMatch.Value.ToUpperInvariant();
                    if (!_unitTags.ContainsKey(unitId))
                        _unitTags[unitId] = new List<string>();

                    if (!_unitTags[unitId].Contains(tag))
                        _unitTags[unitId].Add(tag);
                }
            }
        }
    }

    private void ParseSkills(string message)
    {
        var cleanMsg = StripAnsi(message);
        var match = SkillPattern.Match(cleanMsg);

        if (match.Success)
        {
            var skills = new List<SkillInfo>();

            for (int i = _messageBuffer.Count - 1; i >= Math.Max(0, _messageBuffer.Count - 20); i--)
            {
                var line = StripAnsi(_messageBuffer[i]);
                var skillMatch = SkillPattern.Match(line);

                if (skillMatch.Success)
                {
                    var skill = new SkillInfo
                    {
                        Index = int.Parse(skillMatch.Groups[1].Value),
                        Name = skillMatch.Groups[2].Value,
                        MpCost = double.Parse(skillMatch.Groups[3].Value),
                        Range = int.Parse(skillMatch.Groups[4].Value),
                        CooldownMax = int.Parse(skillMatch.Groups[5].Value),
                        CooldownLeft = int.Parse(skillMatch.Groups[6].Value)
                    };
                    skills.Add(skill);
                }
            }

            if (skills.Count > 0)
            {
                skills.Reverse();
                SkillsUpdated?.Invoke(skills);
            }
        }
    }

    private void CheckTelegraphWarning(string message)
    {
        var cleanMsg = StripAnsi(message);

        if ((cleanMsg.Contains("⚠️") || cleanMsg.Contains("警告") || cleanMsg.Contains("预警")) &&
            (cleanMsg.Contains("BOSS") || cleanMsg.Contains("Boss") || cleanMsg.Contains("boss")))
        {
            TelegraphWarning?.Invoke(cleanMsg);
        }
    }

    private void CheckBossQuote(string message)
    {
        // 检测Boss台词协议标记: [BOSS_QUOTE:eventType:context] 内容
        var cleanLine = StripAnsi(message);
        if (!cleanLine.StartsWith("[BOSS_QUOTE:")) return;
        var match = Regex.Match(cleanLine, @"^\[BOSS_QUOTE:([^:]+):([^\]]*)\]\s*(.+)");
        if (match.Success)
        {
            var eventType = match.Groups[1].Value;
            var context = match.Groups[2].Value;
            var content = match.Groups[3].Value;

            // 提取台词文本（去除 "💬 【XXX】："" 的格式）
            // 支持多种格式：💬 【Boss名】："台词" 或 💬 【Boss名】: "台词" 或直接 "台词"
            string quote = content;

            // 尝试匹配 💬 【XXX】："台词" 或 💬 【XXX】: "台词"
            var quoteMatch1 = Regex.Match(content, @"💬\s*【[^】]+】\s*[：:]\s*[\""]([^\""]+)[\""]");
            if (quoteMatch1.Success)
            {
                quote = quoteMatch1.Groups[1].Value;
            }
            else
            {
                // 尝试匹配直接的引号内容 "台词"
                var quoteMatch2 = Regex.Match(content, @"[\""]([^\""]+)[\""]");
                if (quoteMatch2.Success)
                {
                    quote = quoteMatch2.Groups[1].Value;
                }
                else
                {
                    // 移除可能的前缀标记
                    quote = Regex.Replace(content, @"^💬\s*【[^】]+】\s*[：:]\s*", "").Trim();
                }
            }

            // 协议层去重：检查提取后的纯台词内容和时间间隔
            var now = DateTime.Now;
            var timeSinceLastQuote = now - _lastBossQuoteTime;

            if (_lastBossQuote == quote && timeSinceLastQuote.TotalMilliseconds < 15000)
            {
                // 相同台词且间隔小于5秒，认为是重复，跳过
                System.Diagnostics.Debug.WriteLine($"[CheckBossQuote] 跳过重复台词: {quote} (间隔: {timeSinceLastQuote.TotalMilliseconds}ms)");
                return;
            }

            _lastBossQuote = quote;
            _lastBossQuoteTime = now;

            System.Diagnostics.Debug.WriteLine($"[CheckBossQuote] 触发台词: {quote}");

            BossQuote?.Invoke(new BossQuoteInfo
            {
                Quote = quote,
                EventType = eventType,
                Context = context,
                RawMessage = content
            });
        }
    }

    /// <summary>
    /// 处理输入提示（这是回合开始的可靠信号）
    /// </summary>
    public void HandlePrompt()
    {
        // PROMPT是回合开始的可靠指示器
        TurnStarted?.Invoke();
        NeedInput?.Invoke();
    }

    /// <summary>
    /// 尝试从消息缓冲区解析游戏状态
    /// </summary>
    private GameStateInfo? TryParseGameState()
    {
        var legendStartIndex = -1;
        var legendEndIndex = -1;
        var gridStartIndex = -1;
        var gridEndIndex = -1;
        int day = _lastDay, phase = _lastPhase;
        int widthHint = 0, heightHint = 0, rowLenMax = 0;
        int headerIndex = -1;

        for (int i = _messageBuffer.Count - 1; i >= Math.Max(0, _messageBuffer.Count - 50); i--)
        {
            var line = _messageBuffer[i];
            var cleanLine = StripAnsi(line);

            // Extra delimiters for CN locale: help the parser lock legend range earlier
            if (legendEndIndex == -1 && (cleanLine.Contains("命令：") || cleanLine.Contains("命令:")))
            {
                legendEndIndex = i - 1;
            }
            if (legendStartIndex == -1 && legendEndIndex != -1 && (cleanLine.Contains("图例") || cleanLine.Contains("状态")))
            {
                legendStartIndex = i + 1;
                break;
            }

            if (cleanLine.Contains("LB_FATE") && cleanLine.Contains("Day") && cleanLine.Contains("Phase"))
            {
                var dayMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Day\s+(\d+)");
                var phaseMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Phase\s+(\d+)");
                if (dayMatch.Success) day = int.Parse(dayMatch.Groups[1].Value);
                if (phaseMatch.Success) phase = int.Parse(phaseMatch.Groups[1].Value);
                if (headerIndex == -1) headerIndex = i;
            }

            // Try to extract width from border line: +-----+
            var ts = cleanLine.Trim();
            if (ts.StartsWith("+") && ts.EndsWith("+") && ts.Length >= 3)
            {
                var inner = ts.Substring(1, ts.Length - 2);
                bool allDash = true;
                for (int j = 0; j < inner.Length; j++) if (inner[j] != '-') { allDash = false; break; }
                if (allDash) widthHint = Math.Max(widthHint, inner.Length);
            }

            if (cleanLine.TrimStart().StartsWith("+---"))
            {
                if (gridEndIndex == -1)
                {
                    gridEndIndex = i - 1;
                }
                else if (gridStartIndex == -1)
                {
                    gridStartIndex = i + 1;
                }
            }
        }

        for (int i = _messageBuffer.Count - 1; i >= Math.Max(0, _messageBuffer.Count - 50); i--)
        {
            var line = _messageBuffer[i];
            var cleanLine = StripAnsi(line);

            if (cleanLine.Contains("Commands:") || cleanLine.Contains("命令："))
            {
                if (legendEndIndex == -1)
                {
                    legendEndIndex = i - 1;
                }
            }

            // 查找"图例 / 状态："标题行来确定图例开始位置
            if ((cleanLine.StartsWith("Legend") || cleanLine.Contains("Legend") ||
                 cleanLine.Contains("图例") || cleanLine.Contains("状态：")) && legendStartIndex == -1 && legendEndIndex != -1)
            {
                legendStartIndex = i + 1;  // 图例内容从下一行开始
                break;
            }
        }

        var highlightedCells = new List<(int X, int Y)>();
        var gridUnits = new Dictionary<string, (int X, int Y)>(); // 从网格解析的单位位置
        if (gridStartIndex != -1 && gridEndIndex != -1 && gridStartIndex <= gridEndIndex)
        {
            highlightedCells = ParseGridHighlights(gridStartIndex, gridEndIndex);
            gridUnits = ParseGridUnits(gridStartIndex, gridEndIndex);
            for (int i = gridStartIndex; i <= gridEndIndex && i < _messageBuffer.Count; i++)
            {
                var line2 = StripAnsi(_messageBuffer[i]);
                int q1 = line2.IndexOf('|');
                if (q1 < 0) continue;
                int q2 = line2.IndexOf('|', q1 + 1);
                if (q2 <= q1) continue;
                var yPart2 = line2.Substring(q2 + 1).Trim();
                if (int.TryParse(yPart2, out var y2)) heightHint = Math.Max(heightHint, y2 + 1);
                var cells2 = line2.Substring(q1 + 1, q2 - q1 - 1);
                rowLenMax = Math.Max(rowLenMax, cells2.Length);
            }
        }
        else
        {
            // 松散模式：即使未发现完整边框，也尝试解析最近到达的棋盘行
            int begin = headerIndex >= 0 ? headerIndex : Math.Max(0, _messageBuffer.Count - 50);
            int end = _messageBuffer.Count - 1;
            for (int i = begin; i <= end; i++)
            {
                var line = StripAnsi(_messageBuffer[i]);
                // Try width from digits line: leading space + digits only
                if (line.StartsWith(" "))
                {
                    var digits = line.TrimEnd();
                    bool okDigits = true;
                    for (int j = 1; j < digits.Length; j++) if (!char.IsDigit(digits[j])) { okDigits = false; break; }
                    if (okDigits && digits.Length > 1) widthHint = Math.Max(widthHint, digits.Length - 1);
                }
                int p1 = line.IndexOf('|');
                if (p1 < 0) continue;
                int p2 = line.IndexOf('|', p1 + 1);
                if (p2 <= p1) continue;
                var yPart = line.Substring(p2 + 1).Trim();
                if (!int.TryParse(yPart, out var y)) continue;
                var cells = line.Substring(p1 + 1, p2 - p1 - 1);
                rowLenMax = Math.Max(rowLenMax, cells.Length);
                heightHint = Math.Max(heightHint, y + 1);
                for (int x = 0; x < cells.Length; x++)
                {
                    var ch = cells[x];
                    if (char.IsDigit(ch)) gridUnits[$"P{ch}"] = (x, y);
                    else if (ch == 'B') gridUnits["BOSS"] = (x, y);
                    else if (ch == 'o' || ch == 'x' || ch == '+') highlightedCells.Add((x, y));
                }
            }
        }

        if (legendStartIndex != -1 && legendEndIndex != -1 && legendStartIndex <= legendEndIndex)
        {
            var units = new List<UnitInfo>();

            System.Diagnostics.Debug.WriteLine($"[GameProtocolHandler] 图例范围: {legendStartIndex} - {legendEndIndex}");

            // 调试：输出图例前后5行的内容
            System.Diagnostics.Debug.WriteLine($"[GameProtocolHandler] ===== 图例周围内容 =====");
            for (int i = Math.Max(0, legendStartIndex - 5); i <= Math.Min(_messageBuffer.Count - 1, legendEndIndex + 5); i++)
            {
                var marker = (i >= legendStartIndex && i <= legendEndIndex) ? ">>>" : "   ";
                System.Diagnostics.Debug.WriteLine($"{marker} [{i}]: {StripAnsi(_messageBuffer[i])}");
            }
            System.Diagnostics.Debug.WriteLine($"[GameProtocolHandler] ===== 结束 =====");

            // 首先从图例解析详细信息（当前玩家+Boss）
            for (int i = legendStartIndex; i <= legendEndIndex && i < _messageBuffer.Count; i++)
            {
                var line = _messageBuffer[i];
                System.Diagnostics.Debug.WriteLine($"[GameProtocolHandler] 解析图例行 {i}: {StripAnsi(line)}");
                var unit = ParseUnitLegend(line);
                if (unit != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[GameProtocolHandler] 从图例解析到单位: {unit.Id}");
                    units.Add(unit);
                }
            }

            // 然后从网格添加其他单位（只有位置信息）
            System.Diagnostics.Debug.WriteLine($"[GameProtocolHandler] 从网格解析到 {gridUnits.Count} 个单位");
            foreach (var (unitId, pos) in gridUnits)
            {
                System.Diagnostics.Debug.WriteLine($"[GameProtocolHandler] 网格单位: {unitId} at ({pos.X},{pos.Y})");

                var symbol = unitId == "BOSS" ? "B" : unitId.Replace("P", "");

                // 按位置+Symbol去重：如果图例中已有相同位置和Symbol的单位，跳过
                if (units.Any(u => u.X == pos.X && u.Y == pos.Y && u.Symbol == symbol))
                {
                    System.Diagnostics.Debug.WriteLine($"[GameProtocolHandler] 跳过 {unitId} at ({pos.X},{pos.Y}) - 图例中已有Symbol={symbol}的单位在此位置");
                    continue;
                }

                // 添加只有位置信息的单位
                // 注意：从网格补充的单位不在图例中，说明不是当前玩家
                // 图例只包含"当前玩家 + Boss"，其他玩家只在网格中显示
                var isOffline = unitId.StartsWith("!P");
                var actualId = isOffline ? unitId.Substring(1) : unitId;
                bool isBossUnit = unitId == "BOSS";

                // 从网格补充的玩家单位不是当前玩家（已在图例中的才是当前玩家）
                bool isAlly = false;

                System.Diagnostics.Debug.WriteLine($"[GameProtocolHandler] 添加网格单位: {actualId} (Symbol={symbol}, IsAlly={isAlly}, IsBoss={isBossUnit})");

                units.Add(new UnitInfo
                {
                    Id = actualId,
                    Name = actualId,
                    ClassName = "Unknown", // 未知职业
                    X = pos.X,
                    Y = pos.Y,
                    HP = 1, // 假设存活（因为在网格上显示）
                    MaxHP = 100, // 未知最大HP
                    MP = 0,
                    MaxMP = 10,
                    IsAlly = isAlly,  // 网格补充的单位不是当前玩家
                    Symbol = symbol,
                    Tags = new List<string>(),
                    IsOffline = isOffline
                });
            }

            if (units.Count > 0)
            {
                // Auto-size grid from parsed positions (fallback to defaults)
                int unitMaxX = gridUnits.Count > 0 ? gridUnits.Max(kv => kv.Value.X) + 1 : 0;
                int unitMaxY = gridUnits.Count > 0 ? gridUnits.Max(kv => kv.Value.Y) + 1 : 0;
                int finalW = Math.Max(widthHint, Math.Max(rowLenMax, unitMaxX));
                int finalH = Math.Max(heightHint, unitMaxY);
                var result = new GameStateInfo
                {
                    GridWidth = finalW > 0 ? finalW : 25,
                    GridHeight = finalH > 0 ? finalH : 15,
                    Units = units,
                    HighlightedCells = highlightedCells,
                    Day = day,
                    Phase = phase
                };
                _lastDay = day; _lastPhase = phase;
                return result;
            }
        }

        // Fallback：当未能识别图例区域时，尝试从当前帧中直接提取单位；若仍无，则用网格补齐
        {
            var units = new List<UnitInfo>();

            int startScan = (gridEndIndex != -1) ? Math.Min(gridEndIndex + 1, _messageBuffer.Count - 1)
                                                 : (headerIndex != -1 ? headerIndex : Math.Max(0, _messageBuffer.Count - 50));
            int endScan = _messageBuffer.Count - 1;
            bool afterLegendHeader = false;
            for (int i = startScan; i <= endScan; i++)
            {
                var cl = StripAnsi(_messageBuffer[i]);
                if (cl.Contains("Legend") || cl.Contains("图例") || cl.Contains("状态")) { afterLegendHeader = true; continue; }
                if (afterLegendHeader && (cl.Contains("Commands:") || cl.Contains("命令：") || cl.Contains("命令:") || string.IsNullOrWhiteSpace(cl))) break;
                var u = ParseUnitLegend(_messageBuffer[i]);
                if (u != null) units.Add(u);
            }

            if (units.Count == 0 && headerIndex != -1)
            {
                for (int i = headerIndex; i < _messageBuffer.Count; i++)
                {
                    var u = ParseUnitLegend(_messageBuffer[i]);
                    if (u != null) units.Add(u);
                }
            }

            if (gridUnits.Count > 0)
            {
                foreach (var (unitId, pos) in gridUnits)
                {
                    var symbol = unitId == "BOSS" ? "B" : unitId.Replace("P", "");
                    if (units.Any(u => u.X == pos.X && u.Y == pos.Y && u.Symbol == symbol)) continue;
                    var isOffline = unitId.StartsWith("!P");
                    var actualId = isOffline ? unitId.Substring(1) : unitId;
                    units.Add(new UnitInfo
                    {
                        Id = actualId,
                        Name = actualId,
                        ClassName = "Unknown",
                        X = pos.X,
                        Y = pos.Y,
                        HP = 1,
                        MaxHP = 100,
                        MP = 0,
                        MaxMP = 10,
                        IsAlly = false,
                        Symbol = symbol,
                        Tags = new List<string>(),
                        IsOffline = isOffline
                    });
                }
            }

            if (units.Count > 0)
            {
                int unitMaxX2 = gridUnits.Count > 0 ? gridUnits.Max(kv => kv.Value.X) + 1 : 0;
                int unitMaxY2 = gridUnits.Count > 0 ? gridUnits.Max(kv => kv.Value.Y) + 1 : 0;
                int finalW2 = Math.Max(widthHint, Math.Max(rowLenMax, unitMaxX2));
                int finalH2 = Math.Max(heightHint, unitMaxY2);
                var result = new GameStateInfo
                {
                    GridWidth = finalW2 > 0 ? finalW2 : 25,
                    GridHeight = finalH2 > 0 ? finalH2 : 15,
                    Units = units,
                    HighlightedCells = highlightedCells,
                    Day = day,
                    Phase = phase
                };
                _lastDay = day; _lastPhase = phase;
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// 解析地图网格中的高亮格子（标记为'o'）
    /// </summary>
    private List<(int X, int Y)> ParseGridHighlights(int startIndex, int endIndex)
    {
        var highlights = new List<(int X, int Y)>();

        for (int i = startIndex; i <= endIndex && i < _messageBuffer.Count; i++)
        {
            var line = _messageBuffer[i];
            var cleanLine = StripAnsi(line);

            var parts = cleanLine.Split('|');
            if (parts.Length < 2) continue;

            int y = -1;
            string cells;

            if (parts.Length >= 3 && int.TryParse(parts[0].Trim(), out int leftY))
            {
                y = leftY;
                cells = parts[1];
            }
            else if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1].Trim(), out int rightY))
            {
                y = rightY;
                cells = parts[1];
            }
            else
            {
                continue;
            }

            for (int x = 0; x < cells.Length; x++)
            {
                if (cells[x] == 'o' || cells[x] == 'x' || cells[x] == '+')
                {
                    highlights.Add((x, y));
                }
            }
        }

        return highlights;
    }

    /// <summary>
    /// 解析地图网格中的单位位置（标记为数字、B、!等）
    /// </summary>
    private Dictionary<string, (int X, int Y)> ParseGridUnits(int startIndex, int endIndex)
    {
        var units = new Dictionary<string, (int X, int Y)>();

        for (int i = startIndex; i <= endIndex && i < _messageBuffer.Count; i++)
        {
            var line = _messageBuffer[i];
            var cleanLine = StripAnsi(line);

            var parts = cleanLine.Split('|');
            if (parts.Length < 2) continue;

            int y = -1;
            string cells;

            if (parts.Length >= 3 && int.TryParse(parts[0].Trim(), out int leftY))
            {
                y = leftY;
                cells = parts[1];
            }
            else if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1].Trim(), out int rightY))
            {
                y = rightY;
                cells = parts[1];
            }
            else
            {
                continue;
            }

            for (int x = 0; x < cells.Length; x++)
            {
                var ch = cells[x];

                // 解析玩家单位 (1-9)
                if (char.IsDigit(ch))
                {
                    var unitId = $"P{ch}";
                    units[unitId] = (x, y);
                }
                // 解析Boss (B)
                else if (ch == 'B')
                {
                    units["BOSS"] = (x, y);
                }
                // 解析离线玩家 (!)
                else if (ch == '!' && x + 1 < cells.Length && char.IsDigit(cells[x + 1]))
                {
                    // 注意：!后面应该跟数字，但服务器只发送!，所以需要特殊处理
                    // 暂时标记为未知离线玩家
                    var unitId = $"!P{cells[x + 1]}";
                    units[unitId] = (x, y);
                }
            }
        }

        return units;
    }

    /// <summary>
    /// 去除ANSI颜色代码
    /// </summary>
    private static string StripAnsi(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"\x1B\[[0-9;?]*[ -/]*[@-~]", string.Empty);
    }

    /// <summary>
    /// 解析单位图例行
    /// 格式示例: " 1: P1 Saber      HP[##########](36/36) MP=3 Pos=(10,0)"
    /// </summary>
    private UnitInfo? ParseUnitLegend(string line)
    {
        var cleanLine = StripAnsi(line);

        var match = UnitLegendPattern.Match(cleanLine);
        if (!match.Success)
        {
            // 调试：输出不匹配的行
            if (cleanLine.Contains("HP[") && cleanLine.Contains("MP="))
            {
                System.Diagnostics.Debug.WriteLine($"[ParseUnitLegend] 正则不匹配: {cleanLine}");
            }
            return null;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"[ParseUnitLegend] 成功匹配: {cleanLine}");
            var symbol = match.Groups[1].Value;
            var id = match.Groups[2].Value.Trim();  // 保留完整ID（包括Boss的【】）
            var className = match.Groups[3].Value.Trim();
            var hp = int.Parse(match.Groups[4].Value);
            var maxHp = int.Parse(match.Groups[5].Value);
            var mp = double.Parse(match.Groups[6].Value);
            var x = int.Parse(match.Groups[7].Value);
            var y = int.Parse(match.Groups[8].Value);

            bool isBoss = symbol == "B";
            bool isOffline = symbol == "!";
            // 重要：从图例解析的单位，如果不是Boss，就是当前玩家
            // 因为服务器只发送viewerPid（当前玩家）的详细信息到图例区域
            bool isCurrentPlayer = !isBoss && (char.IsDigit(symbol[0]) || isOffline);

            // 调试日志：输出Symbol信息
            System.Diagnostics.Debug.WriteLine($"[ParseUnitLegend] Symbol='{symbol}', Id='{id}', ClassName='{className}', IsCurrentPlayer={isCurrentPlayer}, IsBoss={isBoss}");

            var tags = _unitTags.TryGetValue(id, out var unitTags) ? new List<string>(unitTags) : new List<string>();

            // 计算MaxMP：首次看到单位时，将当前MP作为MaxMP（初始MP）
            int currentMp = (int)Math.Ceiling(mp);

            // 第一次看到这个单位时，记录初始MP作为MaxMP
            if (!_unitMaxMp.ContainsKey(id))
            {
                _unitMaxMp[id] = currentMp;
            }

            // 使用初始MP作为MaxMP
            int maxMp = _unitMaxMp[id];

            return new UnitInfo
            {
                Id = id,
                Name = id, // Boss的名字在这里（如【泰坦尼亚】）
                ClassName = className,
                X = x,
                Y = y,
                HP = hp,
                MaxHP = maxHp,
                MP = currentMp,
                MaxMP = maxMp,
                IsAlly = isCurrentPlayer,  // 在图例中：非Boss = 当前玩家
                Symbol = symbol,
                Tags = tags,
                IsOffline = isOffline
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 解析游戏状态信息（公开API）
    /// </summary>
    public GameStateInfo? ParseGameState(string message)
    {
        return TryParseGameState();
    }
}

/// <summary>
/// 游戏状态信息
/// </summary>
public record GameStateInfo
{
    public int GridWidth { get; init; }
    public int GridHeight { get; init; }
    public List<UnitInfo> Units { get; init; } = new();
    public List<(int X, int Y)> HighlightedCells { get; init; } = new();
    public int Day { get; init; } = 1;
    public int Phase { get; init; } = 1;

    public int CurrentTurn { get; init; }
    public string? CurrentPlayerName { get; init; }
    public List<TerrainInfo> Terrain { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// 单位信息
/// </summary>
public record UnitInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public int HP { get; init; }
    public int MaxHP { get; init; }
    public int MP { get; init; }
    public int MaxMP { get; init; }
    public bool IsAlly { get; init; }
    public string Symbol { get; init; } = "";
    public string ClassName { get; init; } = "";
    public List<string> Tags { get; init; } = new();
    public bool IsOffline { get; init; } = false;

    public int Level { get; init; } = 1;
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int Speed { get; init; }
    public List<string> Skills { get; init; } = new();
    public List<StatusEffect> StatusEffects { get; init; } = new();
    public Dictionary<string, object> CustomData { get; init; } = new();
}

/// <summary>
/// 地形信息
/// </summary>
public record TerrainInfo
{
    public int X { get; init; }
    public int Y { get; init; }
    public string Type { get; init; } = ""; // 平原、山地、水域等
    public int MoveCost { get; init; } = 1;
    public bool IsBlocking { get; init; } = false;
}

/// <summary>
/// 状态效果
/// </summary>
public record StatusEffect
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public int Duration { get; init; }
    public Dictionary<string, object> Effects { get; init; } = new();
}

/// <summary>
/// 技能信息
/// </summary>
public record SkillInfo
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public double MpCost { get; init; }
    public int Range { get; init; }
    public int CooldownMax { get; init; }
    public int CooldownLeft { get; init; }
    public bool IsAvailable => CooldownLeft == 0;
}

/// <summary>
/// Boss台词信息
/// </summary>
public record BossQuoteInfo
{
    public string Quote { get; init; } = "";
    public string EventType { get; init; } = ""; // turn_start, turn_end, skill, hp_threshold
    public string Context { get; init; } = ""; // 技能名称或HP百分比
    public string RawMessage { get; init; } = "";
}
