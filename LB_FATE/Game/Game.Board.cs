using ETBBS;

namespace LB_FATE;

partial class Game
{
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
        lines.Add($"{AnsiColor.Bold}{AnsiColor.Yellow}Legend / Status:{AnsiColor.Reset}");
        var idsToShow = viewerPid is null ? playerIds.AsEnumerable() : new[] { viewerPid };
        // In boss mode, also show boss status
        if (bossMode && state.Units.ContainsKey(bossId) && GetInt(bossId, Keys.Hp, 0) > 0)
        {
            idsToShow = idsToShow.Append(bossId);
        }
        foreach (var pid in idsToShow)
        {
            var u = state.Units[pid];
            var hp = GetInt(pid, Keys.Hp, 0);
            var maxHp = GetInt(pid, Keys.MaxHp, hp);
            object? mpObj = u.Vars.TryGetValue(Keys.Mp, out var mpv) ? mpv : null;
            string mpStr = mpObj is double d ? d.ToString("0.##") : (mpObj?.ToString() ?? "0");
            var pos = (Coord)u.Vars[Keys.Pos];
            bool isOffline = endpoints.Count > 0 && !endpoints.ContainsKey(pid) && pid != bossId;
            var head = isOffline ? "!" : symbolOf[pid].ToString();
            var offMark = isOffline ? $"{AnsiColor.Red} (offline){AnsiColor.Reset}" : string.Empty;
            var displayName = (bossMode && pid == bossId) ? $"{AnsiColor.Bold}{AnsiColor.BrightRed}【{bossName}】{AnsiColor.Reset}" : pid;
            var hpColor = GetHpColor(hp, maxHp);
            var hpBar = Bar(hp, maxHp, 10);
            lines.Add($" {GetAnsiColorForClass(classOf[pid])}{head}{AnsiColor.Reset}: {displayName} {AnsiColor.Cyan}{classOf[pid],-10}{AnsiColor.Reset} {hpColor}HP[{hpBar}]({hp}/{maxHp}){AnsiColor.Reset} {AnsiColor.BrightBlue}MP={mpStr}{AnsiColor.Reset} {AnsiColor.Gray}Pos={pos}{AnsiColor.Reset}{offMark}");
        }
        lines.Add("");
        lines.Add($"{AnsiColor.Green}Commands:{AnsiColor.Reset} move x y | attack P# | skills | use <n> [P#|x y|up|down|left|right] | info | hint move | pass | help | quit");
        lines.Add($"{AnsiColor.Yellow}Costs   :{AnsiColor.Reset} Move 0.5 MP; Attack 0.5 MP");
        lines.Add($"{AnsiColor.Dim}Note    : Each player acts once per phase.{AnsiColor.Reset}");
        // Recent logs: 合并公共日志 + 当前观者的私有日志（仅 debug 写入）
        var mergedLogs = new List<string>();
        mergedLogs.AddRange(publicLog);
        if (viewerPid is not null && privateLog.TryGetValue(viewerPid, out var priv)) mergedLogs.AddRange(priv);
        if (mergedLogs.Count > 0)
        {
            lines.Add($"{AnsiColor.Bold}{AnsiColor.Magenta}Recent:{AnsiColor.Reset}");
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

    private void BroadcastBoard(int day, int phase)
    {
        if (endpoints.Count == 0) return;
        foreach (var kv in endpoints)
        {
            var pid = kv.Key;
            var ep = kv.Value;
            var lines = GetBoardLines(day, phase, includeHighlights: false, viewerPid: pid);
            foreach (var line in lines) ep.SendLine(line);
        }
    }

    private void ShowBoard(int day, int phase)
    {
        Console.Clear();
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
        Console.WriteLine("Legend / Status:");
        var idsToShow = playerIds.AsEnumerable();
        // In boss mode, also show boss status
        if (bossMode && state.Units.ContainsKey(bossId) && GetInt(bossId, Keys.Hp, 0) > 0)
        {
            idsToShow = idsToShow.Append(bossId);
        }
        foreach (var pid in idsToShow)
        {
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
            Console.Write($"HP[{Bar(hp, maxHp, 10)}]({hp}/{maxHp}) MP={mpStr} Pos={pos}{(isOffline ? " (offline)" : string.Empty)}");
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine("Commands: move x y | attack P# | skills | use <n> P# | info | hint move | pass | help | quit");
        Console.WriteLine("Note    : Each player acts once per phase.");
        // 控制台本地模式：显示公共日志（无私有视角）
        var mergedLogs = new List<string>(publicLog);
        if (mergedLogs.Count > 0)
        {
            Console.WriteLine("Recent:");
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
