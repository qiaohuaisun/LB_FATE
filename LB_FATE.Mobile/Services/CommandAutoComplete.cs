namespace LB_FATE.Mobile.Services;

public record UnitBrief(string Id, int X, int Y, bool IsAlly, bool IsAlive, bool IsOffline);
public record AutoCompleteContext(int PlayerX, int PlayerY, IReadOnlyList<UnitBrief> Units, IReadOnlyList<SkillInfo> Skills);

public static class CommandAutoComplete
{
    private static readonly string[] Commands = new[] { "move", "attack", "skills", "use", "info", "hm", "pass", "help", "quit" };

    public static List<string> GetCompletions(string input, AutoCompleteContext ctx)
    {
        input ??= string.Empty;
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hasTrailingSpace = input.EndsWith(' ');

        // base lists
        var aliveEnemies = ctx.Units.Where(u => !u.IsAlly && u.IsAlive && !u.IsOffline).ToList();
        var aliveAllies = ctx.Units.Where(u => u.IsAlly && u.IsAlive).ToList();
        var unitIds = ctx.Units.Select(u => u.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // No input or typing first token
        if (parts.Length == 0 || (parts.Length == 1 && !hasTrailingSpace))
        {
            var prefix = parts.Length == 0 ? string.Empty : parts[0].ToLowerInvariant();
            return Commands.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                           .OrderBy(c => c)
                           .ToList();
        }

        var cmd = parts[0].ToLowerInvariant();

        // If still on first arg but user typed space, suggest based on command
        if (parts.Length == 1 && hasTrailingSpace)
        {
            return SuggestionsForCommand(cmd, unitIds, ctx.Skills);
        }

        // Helpers
        static int Dist(int ax, int ay, int bx, int by) => Math.Abs(ax - bx) + Math.Abs(ay - by);
        var nearestEnemies = aliveEnemies.OrderBy(u => Dist(ctx.PlayerX, ctx.PlayerY, u.X, u.Y)).Select(u => u.Id).ToList();

        if (cmd is "attack" or "a")
        {
            // Suggest nearest enemies first; filter by typed prefix if present
            var prefix = parts.Length >= 2 ? parts[1] : string.Empty;
            var list = (string.IsNullOrWhiteSpace(prefix)
                ? nearestEnemies
                : nearestEnemies.Where(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            // Ensure BOSS 在顶部（若存在）
            if (list.Remove("BOSS")) list.Insert(0, "BOSS");
            return list.Take(10).ToList();
        }

        if (cmd is "use" or "u")
        {
            // If selecting skill index
            if (parts.Length == 2 && !hasTrailingSpace)
            {
                var prefix = parts[1];
                var idxList = ctx.Skills.Select(s => s.Index.ToString())
                                        .Where(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                        .DefaultIfEmpty("0")
                                        .ToList();
                return idxList.Take(10).ToList();
            }

            // If have skill index, tailor targets by range
            int range = 0;
            if (parts.Length >= 2 && int.TryParse(parts[1], out var sidx))
            {
                var skill = ctx.Skills.FirstOrDefault(s => s.Index == sidx);
                if (skill != null) range = Math.Max(0, skill.Range);
            }

            var suggestions = new List<string>();
            // unit targets by range (nearest first)
            IEnumerable<UnitBrief> candidates = aliveEnemies;
            if (range > 0)
            {
                candidates = aliveEnemies.Where(u => Dist(ctx.PlayerX, ctx.PlayerY, u.X, u.Y) <= range);
            }
            suggestions.AddRange(candidates.OrderBy(u => Dist(ctx.PlayerX, ctx.PlayerY, u.X, u.Y)).Select(u => u.Id).Take(6));

            // directions with steps within range
            if (range > 0)
            {
                suggestions.Add($"up {Math.Min(1, range)}");
                suggestions.Add($"down {Math.Min(1, range)}");
                suggestions.Add($"left {Math.Min(1, range)}");
                suggestions.Add($"right {Math.Min(1, range)}");
                if (range >= 2)
                {
                    suggestions.Add($"up {Math.Min(2, range)}");
                    suggestions.Add($"down {Math.Min(2, range)}");
                    suggestions.Add($"left {Math.Min(2, range)}");
                    suggestions.Add($"right {Math.Min(2, range)}");
                }
            }
            // coordinate hint
            suggestions.Add("x y");
            return suggestions.Distinct().Take(12).ToList();
        }

        if (cmd is "move" or "m")
        {
            // propose adjacent coordinates within board bounds near player
            var res = new List<string>();
            var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            foreach (var (dx, dy) in dirs)
            {
                int nx = ctx.PlayerX + dx, ny = ctx.PlayerY + dy;
                if (nx >= 0 && ny >= 0) res.Add($"{nx} {ny}");
            }
            res.Add("x y");
            return res.Distinct().Take(8).ToList();
        }

        if (cmd is "hint" or "hm")
        {
            return new List<string> { "move" };
        }

        // Default: no suggestions
        return new List<string>();
    }

    private static List<string> SuggestionsForCommand(string cmd, IEnumerable<string> unitIds, IReadOnlyList<SkillInfo> skills)
    {
        switch (cmd)
        {
            case "attack":
            case "a":
                return unitIds.Take(10).ToList();
            case "use":
            case "u":
                var list = skills.Select(s => s.Index.ToString()).ToList();
                if (list.Count == 0) list.Add("0");
                return list;
            case "move":
            case "m":
                return new List<string> { "x y" };
            case "hint":
            case "hm":
                return new List<string> { "move" };
            default:
                return new List<string>();
        }
    }
}
