using ETBBS;

namespace LB_FATE;

public interface IAutoComplete
{
    List<string> GetCompletions(string input);
}

public class AutoComplete : IAutoComplete
{
    private readonly Game game;
    private readonly string playerId;

    public AutoComplete(Game game, string playerId)
    {
        this.game = game;
        this.playerId = playerId;
    }

    public List<string> GetCompletions(string input)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return GetCommandCompletions("");

        var cmd = parts[0].ToLowerInvariant();

        // First word - command completion
        if (parts.Length == 1 && !input.EndsWith(' '))
        {
            return GetCommandCompletions(cmd);
        }

        // After command - context-specific completion
        if (cmd is "attack" or "a")
        {
            return parts.Length == 1 ? GetTargetCompletions("") : GetTargetCompletions(parts[^1]);
        }

        if (cmd is "use" or "u")
        {
            if (parts.Length == 2 && !input.EndsWith(' '))
                return GetSkillIndexCompletions(parts[1]);
            if (parts.Length >= 2)
                return parts.Length == 2 ? GetTargetCompletions("") : GetTargetCompletions(parts[^1]);
        }

        if (cmd is "move" or "m")
        {
            if (parts.Length <= 3)
                return GetCoordinateHints();
        }

        if (cmd == "hint")
        {
            return parts.Length == 1 ? new List<string> { "move" } : new List<string>();
        }

        return new List<string>();
    }

    private List<string> GetCommandCompletions(string prefix)
    {
        var commands = new[] { "move", "attack", "skills", "use", "info", "hint", "pass", "help", "quit" };
        return commands.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                      .OrderBy(c => c)
                      .ToList();
    }

    private List<string> GetTargetCompletions(string prefix)
    {
        var targets = new List<string>();
        var state = game.GetState();

        foreach (var (id, u) in state.Units)
        {
            if (game.GetIntPublic(id, Keys.Hp, 0) <= 0) continue;
            if (id == playerId) continue;
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                targets.Add(id);
        }

        // Add directions
        var directions = new[] { "up", "down", "left", "right" };
        targets.AddRange(directions.Where(d => d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        return targets.OrderBy(t => t).ToList();
    }

    private List<string> GetSkillIndexCompletions(string prefix)
    {
        var state = game.GetState();
        var role = game.GetRoleOf(playerId);
        if (role is null) return new List<string>();

        var indices = new List<string>();
        for (int i = 0; i < role.Skills.Length; i++)
        {
            var idx = i.ToString();
            if (idx.StartsWith(prefix))
                indices.Add(idx);
        }

        return indices;
    }

    private List<string> GetCoordinateHints()
    {
        var state = game.GetState();
        if (!state.Units.ContainsKey(playerId)) return new List<string>();

        var hints = new List<string> { "(hint: enter x y coordinates, e.g., 5 3)" };
        return hints;
    }
}