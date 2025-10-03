using ETBBS;
using static LB_FATE.GameMessages;

namespace LB_FATE;

partial class Game
{
    // 上次广播的单位状态缓存（用于增量更新）
    private Dictionary<string, UnitData> _lastUnitStates = new();

    // ANSI color codes for network clients
    private static class AnsiColor
    {
        public const string Reset = "\x1b[0m";
        public const string Bold = "\x1b[1m";
        public const string Dim = "\x1b[2m";

        // Foreground colors
        public const string Red = "\x1b[31m";
        public const string Green = "\x1b[32m";
        public const string Yellow = "\x1b[33m";
        public const string Blue = "\x1b[34m";
        public const string Magenta = "\x1b[35m";
        public const string Cyan = "\x1b[36m";
        public const string White = "\x1b[37m";
        public const string Gray = "\x1b[90m";
        public const string BrightRed = "\x1b[91m";
        public const string BrightGreen = "\x1b[92m";
        public const string BrightYellow = "\x1b[93m";
        public const string BrightBlue = "\x1b[94m";
        public const string BrightMagenta = "\x1b[95m";
        public const string BrightCyan = "\x1b[96m";
    }

    private void WriteLineTo(string pid, string text)
    {
        Console.WriteLine(text);
        if (endpoints.TryGetValue(pid, out var ep)) ep.SendLine(text);
    }

    private void BroadcastBanner(params string[] lines)
    {
        foreach (var line in lines)
        {
            Console.WriteLine(line);
            foreach (var ep in endpoints.Values)
            {
                try { ep.SendLine(line); } catch { }
            }
        }
    }

    /// <summary>
    /// 广播Boss台词，带特殊视觉效果和协议标记
    /// </summary>
    private void BroadcastBossQuote(string quote, string eventType, string? context = null)
    {
        // 为网络客户端添加特殊协议标记，使用|分隔符避免引号冲突
        // 格式：[BOSS_QUOTE:eventType:context]|台词内容
        var networkMessage = $"[BOSS_QUOTE:{eventType}:{context ?? ""}]|{quote}";

        // 控制台显示增强效果
        var consoleMessage = $"{AnsiColor.Bold}{AnsiColor.BrightRed}💬 【{bossName}】{AnsiColor.Reset}{AnsiColor.BrightYellow}：\"{quote}\"{AnsiColor.Reset}";

        // 根据事件类型添加额外装饰
        switch (eventType)
        {
            case "turn_start":
                consoleMessage = $"{AnsiColor.BrightRed}⚔️  {consoleMessage}  ⚔️{AnsiColor.Reset}";
                break;
            case "skill":
                consoleMessage = $"{AnsiColor.BrightMagenta}✨ {consoleMessage} ✨{AnsiColor.Reset}";
                break;
            case "hp_threshold":
                consoleMessage = $"{AnsiColor.BrightRed}🔥 {consoleMessage} 🔥{AnsiColor.Reset}";
                break;
        }

        // 添加到公共日志（用于后续显示）
        AppendPublic(new[] { networkMessage });

        // 控制台显示
        Console.WriteLine(consoleMessage);

        // 发送到所有网络客户端
        foreach (var ep in endpoints.Values)
        {
            try { ep.SendLine(networkMessage); } catch { }
        }
    }

    private List<string> GetBoardLines(int day, int phase, bool includeHighlights = true, string? viewerPid = null)
    {
        var lines = new List<string>();
        lines.Add($"{AnsiColor.Bold}{AnsiColor.BrightCyan}=== LB_FATE | Day {day} | Phase {phase} ==={AnsiColor.Reset}");
        var grid = new char[height, width];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) grid[y, x] = '.';
        foreach (var (id, u) in state.Units)
        {
            if (GetInt(id, Keys.Hp, 0) <= 0) continue;
            var pos = (Coord)u.Vars[Keys.Pos];
            bool isOffline = endpoints.Count > 0 && !endpoints.ContainsKey(id) && id != bossId;
            grid[pos.Y, pos.X] = isOffline ? '!' : symbolOf[id];
        }
        if (includeHighlights && highlightCells is not null)
        {
            foreach (var c in highlightCells)
            {
                if (c.X >= 0 && c.X < width && c.Y >= 0 && c.Y < height && grid[c.Y, c.X] == '.')
                    grid[c.Y, c.X] = highlightChar;
            }
        }

        // Add X-axis coordinates at the top
        var xAxisLine = new System.Text.StringBuilder();
        xAxisLine.Append($"{AnsiColor.Gray} "); // Align with left border
        for (int x = 0; x < width; x++)
        {
            xAxisLine.Append((x % 10).ToString());
        }
        xAxisLine.Append(AnsiColor.Reset);
        lines.Add(xAxisLine.ToString());

        var border = $"{AnsiColor.Gray}+{new string('-', width)}+{AnsiColor.Reset}";
        lines.Add(border);
        for (int y = 0; y < height; y++)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"{AnsiColor.Gray}|{AnsiColor.Reset}");
            for (int x = 0; x < width; x++)
            {
                var ch = grid[y, x];
                if (char.IsDigit(ch))
                {
                    var pid = "P" + ch;
                    sb.Append(GetAnsiColorForClass(classOf[pid]));
                    sb.Append(ch);
                    sb.Append(AnsiColor.Reset);
                }
                else if (ch == 'B')
                {
                    sb.Append($"{AnsiColor.Bold}{AnsiColor.BrightRed}B{AnsiColor.Reset}");
                }
                else if (ch == 'o' || ch == 'x' || ch == '+')
                {
                    sb.Append($"{AnsiColor.BrightYellow}{ch}{AnsiColor.Reset}");
                }
                else if (ch == '!')
                {
                    sb.Append($"{AnsiColor.BrightRed}!{AnsiColor.Reset}");
                }
                else
                {
                    sb.Append($"{AnsiColor.Dim}{ch}{AnsiColor.Reset}");
                }
            }
            sb.Append($"{AnsiColor.Gray}|{y}{AnsiColor.Reset}");
            lines.Add(sb.ToString());
        }
        lines.Add(border);
        lines.Add($"{AnsiColor.Bold}{AnsiColor.Yellow}图例 / 状态：{AnsiColor.Reset}");
        // 只显示当前观看者的详细信息（保持游戏设计：不能看到其他玩家的HP/MP）
        var idsToShow = viewerPid is null ? playerIds.AsEnumerable() : new[] { viewerPid };
        // In boss mode, also show boss status
        if (bossMode && state.Units.ContainsKey(bossId) && GetInt(bossId, Keys.Hp, 0) > 0)
        {
            idsToShow = idsToShow.Append(bossId);
        }
        foreach (var pid in idsToShow)
        {
            // Skip if unit has been removed (e.g., died and was cleaned up)
            if (!state.Units.ContainsKey(pid)) continue;

            var u = state.Units[pid];
            var hp = GetInt(pid, Keys.Hp, 0);
            var maxHp = GetInt(pid, Keys.MaxHp, hp);
            object? mpObj = u.Vars.TryGetValue(Keys.Mp, out var mpv) ? mpv : null;
            string mpStr = mpObj is double d ? d.ToString("0.##") : (mpObj?.ToString() ?? "0");
            var pos = (Coord)u.Vars[Keys.Pos];
            bool isOffline = endpoints.Count > 0 && !endpoints.ContainsKey(pid) && pid != bossId;
            var head = isOffline ? "!" : symbolOf[pid].ToString();
            var offMark = isOffline ? $"{AnsiColor.Red} (离线){AnsiColor.Reset}" : string.Empty;
            var displayName = (bossMode && pid == bossId) ? $"{AnsiColor.Bold}{AnsiColor.BrightRed}【{bossName}】{AnsiColor.Reset}" : pid;
            var hpColor = GetHpColor(hp, maxHp);
            var hpBar = Bar(hp, maxHp, 10);
            lines.Add($" {GetAnsiColorForClass(classOf[pid])}{head}{AnsiColor.Reset}: {displayName} {AnsiColor.Cyan}{classOf[pid],-10}{AnsiColor.Reset} {hpColor}HP[{hpBar}]({hp}/{maxHp}){AnsiColor.Reset} {AnsiColor.BrightBlue}MP={mpStr}{AnsiColor.Reset} {AnsiColor.Gray}Pos={pos}{AnsiColor.Reset}{offMark}");
        }
        lines.Add("");
        lines.Add($"{AnsiColor.Green}命令：{AnsiColor.Reset} move x y | attack P# | skills | use <n> [P#|x y|up|down|left|right] | info | hint move | pass | help | quit");
        lines.Add($"{AnsiColor.Yellow}消耗    ：{AnsiColor.Reset} 移动 0.5 MP；攻击 0.5 MP");
        lines.Add($"{AnsiColor.Dim}注意    ：每个玩家每阶段行动一次。{AnsiColor.Reset}");
        lines.Add($"{AnsiColor.Dim}提示    ：按 TAB 键自动补全（命令、目标、方向）。{AnsiColor.Reset}");
        // Recent logs: 合并公共日志 + 当前观者的私有日志（仅 debug 写入）
        var mergedLogs = new List<string>();
        mergedLogs.AddRange(publicLog);
        if (viewerPid is not null && privateLog.TryGetValue(viewerPid, out var priv)) mergedLogs.AddRange(priv);
        if (mergedLogs.Count > 0)
        {
            lines.Add($"{AnsiColor.Bold}{AnsiColor.Magenta}最近记录：{AnsiColor.Reset}");
            foreach (var m in mergedLogs.TakeLast(5))
            {
                var coloredMsg = ColorizeLogMessage(m);
                lines.Add($" {AnsiColor.Gray}-{AnsiColor.Reset} " + coloredMsg);
            }
        }
        return lines;
    }

    private string GetAnsiColorForClass(ClassType ct) => ct switch
    {
        ClassType.Saber => AnsiColor.Cyan,
        ClassType.Rider => AnsiColor.BrightYellow,
        ClassType.Archer => AnsiColor.Green,
        ClassType.Lancer => AnsiColor.Blue,
        ClassType.Caster => AnsiColor.Magenta,
        ClassType.Berserker => AnsiColor.BrightRed,
        ClassType.Assassin => AnsiColor.BrightCyan,
        ClassType.Beast => AnsiColor.BrightMagenta,
        ClassType.Grand => AnsiColor.Yellow,
        _ => AnsiColor.White
    };

    private string GetHpColor(int hp, int maxHp)
    {
        if (maxHp == 0) return AnsiColor.Gray;
        double ratio = (double)hp / maxHp;
        if (ratio > 0.7) return AnsiColor.BrightGreen;
        if (ratio > 0.3) return AnsiColor.BrightYellow;
        return AnsiColor.BrightRed;
    }

    private string ColorizeLogMessage(string msg)
    {
        // Boss actions
        if (msg.Contains("【") && msg.Contains("】"))
        {
            msg = msg.Replace("【", $"{AnsiColor.Bold}{AnsiColor.BrightRed}【")
                     .Replace("】", $"】{AnsiColor.Reset}");
        }
        // Emojis and keywords
        if (msg.Contains("💥")) msg = msg.Replace("💥", $"{AnsiColor.BrightRed}💥{AnsiColor.Reset}");
        if (msg.Contains("⚔️")) msg = msg.Replace("⚔️", $"{AnsiColor.BrightYellow}⚔️{AnsiColor.Reset}");
        if (msg.Contains("🚶")) msg = msg.Replace("🚶", $"{AnsiColor.Green}🚶{AnsiColor.Reset}");
        if (msg.Contains("🏃")) msg = msg.Replace("🏃", $"{AnsiColor.Cyan}🏃{AnsiColor.Reset}");
        if (msg.Contains("⚡")) msg = msg.Replace("⚡", $"{AnsiColor.BrightYellow}⚡{AnsiColor.Reset}");
        if (msg.Contains("⚠️")) msg = msg.Replace("⚠️", $"{AnsiColor.BrightYellow}⚠️{AnsiColor.Reset}");

        // Damage/healing
        if (msg.Contains("伤害") || msg.Contains("damage"))
            return $"{AnsiColor.Red}{msg}{AnsiColor.Reset}";
        if (msg.Contains("治疗") || msg.Contains("heal"))
            return $"{AnsiColor.Green}{msg}{AnsiColor.Reset}";

        return msg;
    }

    private void SendBoardTo(string pid, int day, int phase)
    {
        if (!endpoints.TryGetValue(pid, out var ep)) return;
        foreach (var line in GetBoardLines(day, phase, includeHighlights: true, viewerPid: pid)) ep.SendLine(line);
    }

    /// <summary>
    /// 收集当前所有单位的状态
    /// </summary>
    private Dictionary<string, UnitData> CollectCurrentUnits()
    {
        var units = new Dictionary<string, UnitData>();
        foreach (var (id, u) in state.Units)
        {
            try
            {
                if (GetInt(id, Keys.Hp, 0) <= 0) continue; // 跳过已死亡单位

                bool isOffline = endpoints.Count > 0 && !endpoints.ContainsKey(id) && id != bossId;
                bool isCurrentPlayer = id != bossId;

                // 安全获取 class 和 symbol
                string className = classOf.ContainsKey(id) ? classOf[id].ToString() : "Unknown";
                char symbol = symbolOf.ContainsKey(id) ? symbolOf[id] : '?';

                var unitData = GameMessages.ExtractUnitData(
                    id, u, className, symbol,
                    isCurrentPlayer, isOffline);

                units[id] = unitData;
            }
            catch (Exception ex)
            {
                ServerLog($"[CollectCurrentUnits] 处理单位 {id} 时出错: {ex.Message}");
                ServerLog($"[CollectCurrentUnits] 堆栈: {ex.StackTrace}");
            }
        }
        return units;
    }

    /// <summary>
    /// 计算单位状态变化（增量更新）
    /// 隐私策略：只发送当前玩家和Boss的详细信息变化
    /// </summary>
    private Dictionary<string, Dictionary<string, object>> CalculateChanges(
        string viewerPid,
        Dictionary<string, UnitData> currentUnits)
    {
        var changes = new Dictionary<string, Dictionary<string, object>>();

        foreach (var (id, current) in currentUnits)
        {
            bool isCurrentPlayerOrBoss = (id == viewerPid || id == bossId);

            if (!_lastUnitStates.TryGetValue(id, out var last))
            {
                // 新单位: 发送数据（根据隐私策略决定详细程度）
                var unitData = new Dictionary<string, object>
                {
                    ["position"] = current.Position,
                    ["symbol"] = current.Symbol,
                    ["name"] = current.Name,
                    ["class"] = current.Class,
                    ["isAlly"] = current.IsAlly,
                    ["isOffline"] = current.IsOffline
                };

                // 只为当前玩家和Boss发送详细信息
                if (isCurrentPlayerOrBoss)
                {
                    unitData["hp"] = current.Hp;
                    unitData["maxHp"] = current.MaxHp;
                    unitData["mp"] = current.Mp;
                    unitData["maxMp"] = current.MaxMp;
                    unitData["tags"] = current.Tags;
                }
                else
                {
                    // 其他玩家：使用0表示"未知"
                    unitData["hp"] = 0;
                    unitData["maxHp"] = 0;
                    unitData["mp"] = 0;
                    unitData["maxMp"] = 0;
                    unitData["tags"] = Array.Empty<string>();
                }

                changes[id] = unitData;
                continue;
            }

            // 计算变化的字段
            var delta = new Dictionary<string, object>();

            // 位置变化（所有玩家可见）
            if (current.Position.X != last.Position.X || current.Position.Y != last.Position.Y)
                delta["position"] = current.Position;

            // 离线状态变化（所有玩家可见）
            if (current.IsOffline != last.IsOffline)
                delta["isOffline"] = current.IsOffline;

            // HP/MP/Tags变化（仅当前玩家和Boss）
            if (isCurrentPlayerOrBoss)
            {
                if (current.Hp != last.Hp) delta["hp"] = current.Hp;
                if (current.Mp != last.Mp) delta["mp"] = current.Mp;
                if (!current.Tags.SequenceEqual(last.Tags)) delta["tags"] = current.Tags;
            }

            if (delta.Count > 0)
                changes[id] = delta;
        }

        return changes;
    }

    // 追踪每个客户端是否已接收完整状态
    private readonly HashSet<string> _clientsWithFullState = new();

    /// <summary>
    /// 向所有已连接的客户端广播当前棋盘状态
    /// 这个方法会在每次重要的游戏状态变更后自动调用
    /// </summary>
    private void BroadcastBoard(int day, int phase)
    {
        if (endpoints.Count == 0) return;

        try
        {
            ServerLog($"[BroadcastBoard] 开始广播 Day {day} Phase {phase}");

            // 收集当前所有单位状态
            var currentUnits = CollectCurrentUnits();
            ServerLog($"[BroadcastBoard] 收集到 {currentUnits.Count} 个单位");
            bool isFirstBroadcast = _lastUnitStates.Count == 0;

        foreach (var kv in endpoints)
        {
            var pid = kv.Key;
            var ep = kv.Value;
            try
            {
                if (ep.Protocol == ClientProtocol.Json)
                {
                    ServerLog($"[BroadcastBoard] 处理JSON客户端: {pid}");
                    // 判断是否需要发送完整状态
                    bool needFullState = !_clientsWithFullState.Contains(pid) || isFirstBroadcast;

                    if (needFullState)
                    {
                        ServerLog($"[BroadcastBoard] {pid} 需要完整状态");
                        try
                        {
                            // 首次连接或首次广播：发送完整状态
                            // 隐私策略：只发送当前玩家和Boss的详细信息，其他玩家只发送位置和基础信息
                            var visibleUnits = new Dictionary<string, UnitData>();

                            foreach (var (id, unit) in currentUnits)
                            {
                                bool isCurrentPlayerOrBoss = (id == pid || id == bossId);

                                if (isCurrentPlayerOrBoss)
                                {
                                    // 当前玩家和Boss：完整信息
                                    visibleUnits[id] = unit;
                                }
                                else
                                {
                                    // 其他玩家：只发送位置和基础信息（不发送HP/MP/Tags，客户端会使用默认值0）
                                    visibleUnits[id] = unit with
                                    {
                                        Hp = 0,      // 客户端会识别为"未知"
                                        MaxHp = 0,
                                        Mp = 0,
                                        MaxMp = 0,
                                        Tags = Array.Empty<string>()
                                    };
                                }
                            }

                            ServerLog($"[BroadcastBoard] 准备发送 {visibleUnits.Count} 个单位到 {pid}");
                            var msg = GameMessages.BuildFullState(
                                day, phase, width, height, visibleUnits);
                            ServerLog($"[BroadcastBoard] 已构建完整状态消息，准备发送");
                            ep.SendJson(msg);
                            ServerLog($"[BroadcastBoard] 已发送完整状态到 {pid}");

                            _clientsWithFullState.Add(pid);
                            ServerLog($"[BroadcastBoard] Sent full state to {pid} (JSON) - {visibleUnits.Count} units");
                        }
                        catch (Exception ex)
                        {
                            ServerLog($"[BroadcastBoard] 发送完整状态到 {pid} 时出错: {ex.Message}");
                            ServerLog($"[BroadcastBoard] 堆栈: {ex.StackTrace}");
                            throw; // 重新抛出，让外层catch处理
                        }
                    }
                    else
                    {
                        // 增量更新（传递viewerPid以实现隐私策略）
                        var changes = CalculateChanges(pid, currentUnits);

                        if (changes.Count > 0)
                        {
                            var msg = GameMessages.BuildDeltaUpdate(day, phase, changes);
                            ep.SendJson(msg);
                        }
                    }
                }
                else
                {
                    // 文本客户端: 保持原有逻辑
                    ServerLog($"[BroadcastBoard] 处理文本客户端: {pid}");
                    var lines = GetBoardLines(day, phase, includeHighlights: false, viewerPid: pid);
                    ServerLog($"[BroadcastBoard] GetBoardLines 返回 {lines.Count} 行");
                    foreach (var line in lines) ep.SendLine(line);
                    ServerLog($"[BroadcastBoard] 已发送 {lines.Count} 行到 {pid}");
                }
            }
            catch (Exception ex)
            {
                ServerLog($"[BroadcastBoard] Failed to send to {pid}: {ex.Message}");
            }
        }

            // 更新上次状态缓存
            _lastUnitStates = currentUnits;
            ServerLog($"[BroadcastBoard] 广播完成");
        }
        catch (Exception ex)
        {
            ServerLog($"[BroadcastBoard] 严重错误: {ex.Message}");
            ServerLog($"[BroadcastBoard] 堆栈跟踪: {ex.StackTrace}");
        }
    }

    private void ShowBoard(int day, int phase)
    {
        // 临时禁用清屏，保留调试日志
        // Console.Clear();
        Console.WriteLine($"=== LB_FATE | Day {day} | Phase {phase} ===");
        var grid = new char[height, width];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) grid[y, x] = '.';
        foreach (var (id, u) in state.Units)
        {
            if (GetInt(id, Keys.Hp, 0) <= 0) continue;
            var pos = (Coord)u.Vars[Keys.Pos];
            bool isOffline = endpoints.Count > 0 && !endpoints.ContainsKey(id) && id != bossId;
            grid[pos.Y, pos.X] = isOffline ? '!' : symbolOf[id];
        }
        if (highlightCells is not null)
        {
            foreach (var c in highlightCells)
            {
                if (c.X >= 0 && c.X < width && c.Y >= 0 && c.Y < height && grid[c.Y, c.X] == '.')
                    grid[c.Y, c.X] = highlightChar;
            }
        }

        // X-axis coordinates at the top
        Console.Write(" "); // Align with left border
        for (int x = 0; x < width; x++)
        {
            Console.Write((x % 10).ToString());
        }
        Console.WriteLine();

        Console.Write("+"); for (int x = 0; x < width; x++) Console.Write("-"); Console.WriteLine("+");
        for (int y = 0; y < height; y++)
        {
            Console.Write("|");
            for (int x = 0; x < width; x++)
            {
                var ch = grid[y, x];
                var colored = false;
                if (char.IsDigit(ch))
                {
                    var pid = "P" + ch;
                    Console.ForegroundColor = GetColor(classOf[pid]); colored = true;
                }
                else if (ch == 'o' || ch == 'x' || ch == '+')
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow; colored = true;
                }
                else if (ch == '!')
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed; colored = true;
                }
                else if (ch == 'B')
                {
                    Console.ForegroundColor = ConsoleColor.Red; colored = true;
                }
                Console.Write(ch);
                if (colored) Console.ResetColor();
            }
            Console.Write("|");
            Console.WriteLine(y); // Y-axis coordinate at the end
        }
        Console.Write("+"); for (int x = 0; x < width; x++) Console.Write("-"); Console.WriteLine("+");
        Console.WriteLine("图例 / 状态：");
        var idsToShow = playerIds.AsEnumerable();
        // In boss mode, also show boss status
        if (bossMode && state.Units.ContainsKey(bossId) && GetInt(bossId, Keys.Hp, 0) > 0)
        {
            idsToShow = idsToShow.Append(bossId);
        }
        foreach (var pid in idsToShow)
        {
            // Skip if unit has been removed (e.g., died and was cleaned up)
            if (!state.Units.ContainsKey(pid)) continue;

            var u = state.Units[pid];
            var hp = GetInt(pid, Keys.Hp, 0);
            var maxHp = GetInt(pid, Keys.MaxHp, hp);
            object? mpObj = u.Vars.TryGetValue(Keys.Mp, out var mpv) ? mpv : null;
            string mpStr = mpObj is double d ? d.ToString("0.##") : (mpObj?.ToString() ?? "0");
            var pos = (Coord)u.Vars[Keys.Pos];
            bool isOffline = endpoints.Count > 0 && !endpoints.ContainsKey(pid) && pid != bossId;
            var head = isOffline ? "!" : symbolOf[pid].ToString();
            var displayName = (bossMode && pid == bossId) ? $"【{bossName}】" : pid;
            Console.ForegroundColor = GetColor(classOf[pid]); Console.Write($" {head}: {displayName} {classOf[pid],-10} "); Console.ResetColor();
            Console.Write($"HP[{Bar(hp, maxHp, 10)}]({hp}/{maxHp}) MP={mpStr} Pos={pos}{(isOffline ? " (离线)" : string.Empty)}");
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine("命令：move x y | attack P# | skills | use <n> P# | info | hint move | pass | help | quit");
        Console.WriteLine("注意    ：每个玩家每阶段行动一次。");
        // 控制台本地模式：显示公共日志（无私有视角）
        var mergedLogs = new List<string>(publicLog);
        if (mergedLogs.Count > 0)
        {
            Console.WriteLine("最近记录：");
            foreach (var m in mergedLogs.TakeLast(5)) Console.WriteLine(" - " + m);
        }
    }

    private ConsoleColor GetColor(ClassType ct) => ct switch
    {
        ClassType.Saber => ConsoleColor.Cyan,
        ClassType.Rider => ConsoleColor.DarkYellow,
        ClassType.Archer => ConsoleColor.Green,
        ClassType.Lancer => ConsoleColor.Blue,
        ClassType.Caster => ConsoleColor.Magenta,
        ClassType.Berserker => ConsoleColor.Red,
        ClassType.Assassin => ConsoleColor.DarkCyan,
        ClassType.Beast => ConsoleColor.Magenta,
        ClassType.Grand => ConsoleColor.Yellow,
        _ => ConsoleColor.White
    };

    private string Bar(int v, int max, int width)
    {
        max = Math.Max(1, max); v = Math.Max(0, Math.Min(max, v));
        int filled = (int)Math.Round((double)v / max * width);
        return new string('#', filled) + new string('.', width - filled);
    }

    private void AppendPublic(IEnumerable<string> msgs)
    {
        foreach (var m in msgs) publicLog.Add(m);
        while (publicLog.Count > 20) publicLog.RemoveAt(0);
    }

    private void AppendDebugFor(string pid, IEnumerable<string> msgs)
    {
        if (!debugLogs) return;
        if (!privateLog.TryGetValue(pid, out var list)) { list = new List<string>(); privateLog[pid] = list; }
        foreach (var m in msgs) list.Add(m);
        while (list.Count > 20) list.RemoveAt(0);
    }
}
