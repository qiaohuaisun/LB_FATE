namespace LB_FATE;

/// <summary>
/// Simplified auto-completion for network clients (command-only, no game state access)
/// </summary>
public static class ClientAutoComplete
{
    public static List<string> GetCompletions(string input)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return GetCommandCompletions("");

        var cmd = parts[0].ToLowerInvariant();

        // First word - command completion
        if (parts.Length == 1 && !input.EndsWith(' '))
        {
            return GetCommandCompletions(cmd);
        }

        // For network clients, we can only provide hints, not dynamic completions
        if (cmd is "attack" or "a")
        {
            return new List<string> { "(enter target: P1, P2, ..., or direction)" };
        }

        if (cmd is "use" or "u")
        {
            if (parts.Length == 2 && !input.EndsWith(' '))
                return new List<string> { "(enter skill index: 0, 1, 2, ...)" };
            if (parts.Length >= 2)
                return new List<string> { "(enter target: P#, x y, or up/down/left/right)" };
        }

        if (cmd is "move" or "m")
        {
            return new List<string> { "(enter coordinates: x y)" };
        }

        if (cmd == "hint")
        {
            return parts.Length == 1 ? new List<string> { "move" } : new List<string>();
        }

        return new List<string>();
    }

    private static List<string> GetCommandCompletions(string prefix)
    {
        var commands = new[] { "move", "attack", "skills", "use", "info", "hint", "pass", "help", "quit" };
        return commands.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                      .OrderBy(c => c)
                      .ToList();
    }
}