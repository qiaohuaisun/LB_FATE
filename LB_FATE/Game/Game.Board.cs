using ETBBS;

namespace LB_FATE;

partial class Game
{
    private void WriteLineTo(string pid, string text)
    {
        Console.WriteLine(text);
        if (endpoints.TryGetValue(pid, out var ep)) ep.SendLine(text);
    }

    private List<string> GetBoardLines(int day, int phase, bool includeHighlights = true)
    {
        var lines = new List<string>();
        lines.Add($"=== LB_FATE | Day {day} | Phase {phase} ===");
        var grid = new char[height, width];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) grid[y, x] = '.';
        foreach (var (id, u) in state.Units)
        {
            if (GetInt(id, Keys.Hp, 0) <= 0) continue;
            var pos = (Coord)u.Vars[Keys.Pos];
            grid[pos.Y, pos.X] = symbolOf[id];
        }
        if (includeHighlights && highlightCells is not null)
        {
            foreach (var c in highlightCells)
            {
                if (c.X >= 0 && c.X < width && c.Y >= 0 && c.Y < height && grid[c.Y, c.X] == '.')
                    grid[c.Y, c.X] = highlightChar;
            }
        }
        var border = "+" + new string('-', width) + "+";
        lines.Add(border);
        for (int y = 0; y < height; y++)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('|');
            for (int x = 0; x < width; x++) sb.Append(grid[y, x]);
            sb.Append('|');
            lines.Add(sb.ToString());
        }
        lines.Add(border);
        lines.Add("Legend / Status:");
        foreach (var pid in playerIds)
        {
            var u = state.Units[pid];
            var hp = GetInt(pid, Keys.Hp, 0);
            var maxHp = GetInt(pid, Keys.MaxHp, hp);
            object? mpObj = u.Vars.TryGetValue(Keys.Mp, out var mpv) ? mpv : null;
            string mpStr = mpObj is double d ? d.ToString("0.##") : (mpObj?.ToString() ?? "0");
            var pos = (Coord)u.Vars[Keys.Pos];
            lines.Add($" {symbolOf[pid]}: {pid} {classOf[pid],-10} HP[{Bar(hp, maxHp, 10)}]({hp}/{maxHp}) MP={mpStr} Pos={pos}");
        }
        lines.Add("");
        lines.Add("Commands: move x y | attack P# | skills | use <n> [P#|x y|up|down|left|right] | info | hint move|attack | pass | help | quit");
        lines.Add("Costs   : Move 0.5 MP; Attack 0.5 MP");
        lines.Add("Phases 1 & 5: all commands; Phases 2-4: move/pass only.");
        if (recentLog.Count > 0)
        {
            lines.Add("Recent:");
            foreach (var m in recentLog.TakeLast(5)) lines.Add(" - " + m);
        }
        return lines;
    }

    private void SendBoardTo(string pid, int day, int phase)
    {
        if (!endpoints.TryGetValue(pid, out var ep)) return;
        foreach (var line in GetBoardLines(day, phase, includeHighlights: true)) ep.SendLine(line);
    }

    private void BroadcastBoard(int day, int phase)
    {
        if (endpoints.Count == 0) return;
        var lines = GetBoardLines(day, phase, includeHighlights: false);
        foreach (var ep in endpoints.Values)
            foreach (var line in lines) ep.SendLine(line);
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
            grid[pos.Y, pos.X] = symbolOf[id];
        }
        if (highlightCells is not null)
        {
            foreach (var c in highlightCells)
            {
                if (c.X >= 0 && c.X < width && c.Y >= 0 && c.Y < height && grid[c.Y, c.X] == '.')
                    grid[c.Y, c.X] = highlightChar;
            }
        }
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
                Console.Write(ch);
                if (colored) Console.ResetColor();
            }
            Console.WriteLine("|");
        }
        Console.Write("+"); for (int x = 0; x < width; x++) Console.Write("-"); Console.WriteLine("+");
        Console.WriteLine("Legend / Status:");
        foreach (var pid in playerIds)
        {
            var u = state.Units[pid];
            var hp = GetInt(pid, Keys.Hp, 0);
            var maxHp = GetInt(pid, Keys.MaxHp, hp);
            object? mpObj = u.Vars.TryGetValue(Keys.Mp, out var mpv) ? mpv : null;
            string mpStr = mpObj is double d ? d.ToString("0.##") : (mpObj?.ToString() ?? "0");
            var pos = (Coord)u.Vars[Keys.Pos];
            Console.ForegroundColor = GetColor(classOf[pid]); Console.Write($" {symbolOf[pid]}: {pid} {classOf[pid],-10} "); Console.ResetColor();
            Console.Write($"HP[{Bar(hp, maxHp, 10)}]({hp}/{maxHp}) MP={mpStr} Pos={pos}");
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine("Commands: move x y | attack P# | skills | use <n> P# | info | hint move|attack | pass | help | quit");
        Console.WriteLine("Phases 1 & 5: all commands; Phases 2-4: move/pass only.");
        if (recentLog.Count > 0)
        {
            Console.WriteLine("Recent:");
            foreach (var m in recentLog.TakeLast(5)) Console.WriteLine(" - " + m);
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

    private void AppendLog(IEnumerable<string> msgs)
    {
        foreach (var m in msgs) recentLog.Add(m);
        while (recentLog.Count > 10) recentLog.RemoveAt(0);
    }
}
