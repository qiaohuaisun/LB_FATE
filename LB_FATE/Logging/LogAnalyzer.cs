using System.Text.RegularExpressions;

namespace LB_FATE.Logging;

/// <summary>
/// Provides tools for analyzing and querying log files.
/// </summary>
public static class LogAnalyzer
{
    /// <summary>
    /// Represents a parsed log entry.
    /// </summary>
    public record LogEntry(
        DateTime Timestamp,
        string Level,
        string Message,
        string? Exception = null,
        Dictionary<string, string>? Properties = null
    );

    /// <summary>
    /// Parses a log file and returns structured log entries.
    /// </summary>
    public static IEnumerable<LogEntry> ParseLogFile(string filePath)
    {
        if (!File.Exists(filePath))
            yield break;

        // Pattern: 2025-09-30 12:34:56.789 +08:00 [INF] Message
        var pattern = @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[(\w{3})\] (.+)$";
        var regex = new Regex(pattern, RegexOptions.Compiled);

        string? currentException = null;
        LogEntry? currentEntry = null;

        foreach (var line in File.ReadLines(filePath))
        {
            var match = regex.Match(line);
            if (match.Success)
            {
                // Yield previous entry if exists
                if (currentEntry != null)
                {
                    yield return currentEntry with { Exception = currentException };
                    currentException = null;
                }

                // Parse new entry
                var timestamp = DateTime.Parse(match.Groups[1].Value);
                var level = match.Groups[2].Value;
                var message = match.Groups[3].Value;

                currentEntry = new LogEntry(timestamp, level, message);
            }
            else if (currentEntry != null)
            {
                // Continuation line (exception or multiline message)
                currentException = (currentException ?? "") + line + Environment.NewLine;
            }
        }

        // Yield last entry
        if (currentEntry != null)
        {
            yield return currentEntry with { Exception = currentException };
        }
    }

    /// <summary>
    /// Filters log entries by level.
    /// </summary>
    public static IEnumerable<LogEntry> FilterByLevel(IEnumerable<LogEntry> entries, params string[] levels)
    {
        var levelSet = new HashSet<string>(levels, StringComparer.OrdinalIgnoreCase);
        return entries.Where(e => levelSet.Contains(e.Level));
    }

    /// <summary>
    /// Filters log entries by time range.
    /// </summary>
    public static IEnumerable<LogEntry> FilterByTimeRange(IEnumerable<LogEntry> entries, DateTime start, DateTime end)
    {
        return entries.Where(e => e.Timestamp >= start && e.Timestamp <= end);
    }

    /// <summary>
    /// Searches log entries by message content.
    /// </summary>
    public static IEnumerable<LogEntry> Search(IEnumerable<LogEntry> entries, string searchTerm, bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return entries.Where(e => e.Message.Contains(searchTerm, comparison));
    }

    /// <summary>
    /// Finds log entries with exceptions.
    /// </summary>
    public static IEnumerable<LogEntry> FindWithExceptions(IEnumerable<LogEntry> entries)
    {
        return entries.Where(e => !string.IsNullOrWhiteSpace(e.Exception));
    }

    /// <summary>
    /// Generates statistics from log entries.
    /// </summary>
    public static LogStatistics GenerateStatistics(IEnumerable<LogEntry> entries)
    {
        var entryList = entries.ToList();
        var levelCounts = entryList.GroupBy(e => e.Level)
            .ToDictionary(g => g.Key, g => g.Count());

        var exceptionCount = entryList.Count(e => !string.IsNullOrWhiteSpace(e.Exception));

        DateTime? firstTimestamp = entryList.Any() ? entryList.Min(e => e.Timestamp) : null;
        DateTime? lastTimestamp = entryList.Any() ? entryList.Max(e => e.Timestamp) : null;

        return new LogStatistics(
            TotalEntries: entryList.Count,
            LevelCounts: levelCounts,
            ExceptionCount: exceptionCount,
            FirstTimestamp: firstTimestamp,
            LastTimestamp: lastTimestamp,
            Duration: firstTimestamp.HasValue && lastTimestamp.HasValue
                ? lastTimestamp.Value - firstTimestamp.Value
                : null
        );
    }

    /// <summary>
    /// Extracts performance metrics from log entries.
    /// </summary>
    public static IEnumerable<PerformanceMetric> ExtractPerformanceMetrics(IEnumerable<LogEntry> entries)
    {
        // Pattern to extract ElapsedMs from messages like "... in 123.45ms"
        var pattern = @"in (\d+(?:\.\d+)?)ms";
        var regex = new Regex(pattern, RegexOptions.Compiled);

        foreach (var entry in entries)
        {
            var match = regex.Match(entry.Message);
            if (match.Success && double.TryParse(match.Groups[1].Value, out var elapsedMs))
            {
                // Try to extract operation name (everything before " in ")
                var opNameMatch = Regex.Match(entry.Message, @"^(.+?)\s+in\s+\d+");
                var operationName = opNameMatch.Success ? opNameMatch.Groups[1].Value.Trim() : "Unknown";

                yield return new PerformanceMetric(
                    Timestamp: entry.Timestamp,
                    OperationName: operationName,
                    DurationMs: elapsedMs
                );
            }
        }
    }
}

/// <summary>
/// Statistics about log entries.
/// </summary>
public record LogStatistics(
    int TotalEntries,
    Dictionary<string, int> LevelCounts,
    int ExceptionCount,
    DateTime? FirstTimestamp,
    DateTime? LastTimestamp,
    TimeSpan? Duration
);

/// <summary>
/// Represents a performance metric extracted from logs.
/// </summary>
public record PerformanceMetric(
    DateTime Timestamp,
    string OperationName,
    double DurationMs
);

/// <summary>
/// CLI tool for analyzing log files.
/// </summary>
public static class LogAnalyzerCLI
{
    public static void Run(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        var command = args[0].ToLower();
        var logFile = args.Length > 1 ? args[1] : FindLatestLogFile();

        if (string.IsNullOrEmpty(logFile) || !File.Exists(logFile))
        {
            Console.WriteLine($"Error: Log file not found: {logFile}");
            return;
        }

        var entries = LogAnalyzer.ParseLogFile(logFile).ToList();
        Console.WriteLine($"Loaded {entries.Count} log entries from: {logFile}");
        Console.WriteLine();

        switch (command)
        {
            case "stats":
                ShowStatistics(entries);
                break;

            case "errors":
                ShowErrors(entries);
                break;

            case "perf":
                ShowPerformanceMetrics(entries);
                break;

            case "search":
                if (args.Length < 3)
                {
                    Console.WriteLine("Usage: loganalyzer search <logfile> <search term>");
                    return;
                }
                SearchLogs(entries, args[2]);
                break;

            case "tail":
                TailLogs(entries, args.Length > 2 && int.TryParse(args[2], out var n) ? n : 20);
                break;

            default:
                ShowHelp();
                break;
        }
    }

    private static string? FindLatestLogFile()
    {
        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        if (!Directory.Exists(logsDir))
            return null;

        return Directory.GetFiles(logsDir, "lb_fate_*.log")
            .OrderByDescending(f => new FileInfo(f).LastWriteTime)
            .FirstOrDefault();
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Log Analyzer Tool");
        Console.WriteLine();
        Console.WriteLine("Usage: loganalyzer <command> [logfile] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  stats               Show log statistics");
        Console.WriteLine("  errors              Show errors and exceptions");
        Console.WriteLine("  perf                Show performance metrics");
        Console.WriteLine("  search <term>       Search for specific text");
        Console.WriteLine("  tail [n]            Show last n entries (default: 20)");
        Console.WriteLine();
        Console.WriteLine("If logfile is omitted, the latest log file is used.");
    }

    private static void ShowStatistics(List<LogAnalyzer.LogEntry> entries)
    {
        var stats = LogAnalyzer.GenerateStatistics(entries);

        Console.WriteLine("=== Log Statistics ===");
        Console.WriteLine($"Total Entries: {stats.TotalEntries}");
        Console.WriteLine($"Time Range: {stats.FirstTimestamp:yyyy-MM-dd HH:mm:ss} to {stats.LastTimestamp:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Duration: {stats.Duration?.TotalMinutes:F2} minutes");
        Console.WriteLine();

        Console.WriteLine("Entries by Level:");
        foreach (var (level, count) in stats.LevelCounts.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"  {level,5}: {count,6} ({100.0 * count / stats.TotalEntries:F1}%)");
        }

        Console.WriteLine();
        Console.WriteLine($"Exceptions: {stats.ExceptionCount}");
    }

    private static void ShowErrors(List<LogAnalyzer.LogEntry> entries)
    {
        var errors = LogAnalyzer.FilterByLevel(entries, "ERR", "FTL").ToList();

        Console.WriteLine($"=== Errors ({errors.Count}) ===");
        Console.WriteLine();

        foreach (var error in errors.Take(50))
        {
            Console.WriteLine($"[{error.Timestamp:HH:mm:ss}] {error.Message}");
            if (!string.IsNullOrWhiteSpace(error.Exception))
            {
                Console.WriteLine(error.Exception.Trim());
            }
            Console.WriteLine();
        }
    }

    private static void ShowPerformanceMetrics(List<LogAnalyzer.LogEntry> entries)
    {
        var metrics = LogAnalyzer.ExtractPerformanceMetrics(entries).ToList();

        Console.WriteLine($"=== Performance Metrics ({metrics.Count}) ===");
        Console.WriteLine();

        var grouped = metrics.GroupBy(m => m.OperationName)
            .Select(g => new
            {
                Operation = g.Key,
                Count = g.Count(),
                AvgMs = g.Average(m => m.DurationMs),
                MinMs = g.Min(m => m.DurationMs),
                MaxMs = g.Max(m => m.DurationMs),
                TotalMs = g.Sum(m => m.DurationMs)
            })
            .OrderByDescending(x => x.TotalMs);

        Console.WriteLine($"{"Operation",-40} {"Count",8} {"Avg",10} {"Min",10} {"Max",10} {"Total",12}");
        Console.WriteLine(new string('-', 100));

        foreach (var item in grouped)
        {
            Console.WriteLine($"{item.Operation,-40} {item.Count,8} {item.AvgMs,10:F2}ms {item.MinMs,10:F2}ms {item.MaxMs,10:F2}ms {item.TotalMs,12:F2}ms");
        }
    }

    private static void SearchLogs(List<LogAnalyzer.LogEntry> entries, string searchTerm)
    {
        var results = LogAnalyzer.Search(entries, searchTerm).ToList();

        Console.WriteLine($"=== Search Results for \"{searchTerm}\" ({results.Count}) ===");
        Console.WriteLine();

        foreach (var entry in results.Take(100))
        {
            Console.WriteLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}");
        }

        if (results.Count > 100)
        {
            Console.WriteLine();
            Console.WriteLine($"... and {results.Count - 100} more results");
        }
    }

    private static void TailLogs(List<LogAnalyzer.LogEntry> entries, int count)
    {
        Console.WriteLine($"=== Last {count} Entries ===");
        Console.WriteLine();

        foreach (var entry in entries.TakeLast(count))
        {
            Console.WriteLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}");
        }
    }
}