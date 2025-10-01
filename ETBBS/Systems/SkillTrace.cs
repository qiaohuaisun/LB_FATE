using System.Text;

namespace ETBBS;

/// <summary>
/// Skill execution trace entry for debugging
/// </summary>
public sealed class TraceEntry
{
    public int Step { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, object> Data { get; init; } = new();
    public int Depth { get; init; }

    public override string ToString()
    {
        var indent = new string(' ', Depth * 2);
        var sb = new StringBuilder();
        sb.Append($"{indent}[{Step}] {Type}: {Message}");
        if (Data.Count > 0)
        {
            sb.Append(" {");
            sb.Append(string.Join(", ", Data.Select(kv => $"{kv.Key}={kv.Value}")));
            sb.Append("}");
        }
        return sb.ToString();
    }
}

/// <summary>
/// Skill execution trace collector
/// </summary>
public sealed class SkillTrace
{
    private readonly List<TraceEntry> _entries = new();
    private int _stepCounter = 0;
    private int _depth = 0;
    private readonly bool _enabled;

    public IReadOnlyList<TraceEntry> Entries => _entries;
    public bool IsEnabled => _enabled;

    public SkillTrace(bool enabled = true)
    {
        _enabled = enabled;
    }

    public void BeginScope(string scopeName)
    {
        if (!_enabled) return;
        Log("Scope", $"Enter: {scopeName}");
        _depth++;
    }

    public void EndScope(string scopeName)
    {
        if (!_enabled) return;
        _depth--;
        Log("Scope", $"Exit: {scopeName}");
    }

    public void LogAction(string action, string details, Dictionary<string, object>? data = null)
    {
        if (!_enabled) return;
        Log("Action", $"{action}: {details}", data);
    }

    public void LogCondition(string condition, bool result, Dictionary<string, object>? data = null)
    {
        if (!_enabled) return;
        var combinedData = data ?? new Dictionary<string, object>();
        combinedData["result"] = result;
        Log("Condition", condition, combinedData);
    }

    public void LogSelector(string selector, IEnumerable<string> selected, Dictionary<string, object>? data = null)
    {
        if (!_enabled) return;
        var selectedList = selected.ToList();
        var combinedData = data ?? new Dictionary<string, object>();
        combinedData["count"] = selectedList.Count;
        combinedData["units"] = string.Join(", ", selectedList);
        Log("Selector", selector, combinedData);
    }

    public void LogVariable(string varName, object oldValue, object newValue)
    {
        if (!_enabled) return;
        Log("Variable", $"{varName} changed", new Dictionary<string, object>
        {
            ["from"] = oldValue,
            ["to"] = newValue
        });
    }

    public void LogDamage(string source, string target, int amount, string type)
    {
        if (!_enabled) return;
        Log("Damage", $"{source} → {target}", new Dictionary<string, object>
        {
            ["amount"] = amount,
            ["type"] = type
        });
    }

    public void LogHeal(string target, int amount)
    {
        if (!_enabled) return;
        Log("Heal", $"→ {target}", new Dictionary<string, object>
        {
            ["amount"] = amount
        });
    }

    private void Log(string type, string message, Dictionary<string, object>? data = null)
    {
        if (!_enabled) return;
        _entries.Add(new TraceEntry
        {
            Step = ++_stepCounter,
            Type = type,
            Message = message,
            Data = data ?? new Dictionary<string, object>(),
            Depth = _depth
        });
    }

    public string FormatTrace(bool verbose = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Skill Execution Trace ===");
        sb.AppendLine($"Total steps: {_entries.Count}");
        sb.AppendLine();

        foreach (var entry in _entries)
        {
            if (!verbose && entry.Type == "Scope") continue; // Skip scope entries in non-verbose mode
            sb.AppendLine(entry.ToString());
        }

        return sb.ToString();
    }

    public void Clear()
    {
        _entries.Clear();
        _stepCounter = 0;
        _depth = 0;
    }
}

/// <summary>
/// Extension methods for tracing
/// </summary>
public static class TraceExtensions
{
    private static readonly AsyncLocal<SkillTrace?> _currentTrace = new();

    public static SkillTrace? CurrentTrace
    {
        get => _currentTrace.Value;
        set => _currentTrace.Value = value;
    }

    public static IDisposable? TraceScope(this SkillTrace? trace, string scopeName)
    {
        if (trace == null || !trace.IsEnabled) return null;
        trace.BeginScope(scopeName);
        return new ScopeDisposable(trace, scopeName);
    }

    private sealed class ScopeDisposable : IDisposable
    {
        private readonly SkillTrace _trace;
        private readonly string _scopeName;

        public ScopeDisposable(SkillTrace trace, string scopeName)
        {
            _trace = trace;
            _scopeName = scopeName;
        }

        public void Dispose()
        {
            _trace.EndScope(_scopeName);
        }
    }
}
