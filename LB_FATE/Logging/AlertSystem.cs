using Serilog;
using Serilog.Events;
using Serilog.Sinks.File;
using System.Collections.Concurrent;

namespace LB_FATE.Logging;

/// <summary>
/// Monitors logs and triggers alerts based on configurable rules.
/// </summary>
public sealed class AlertSystem
{
    private readonly List<AlertRule> _rules = new();
    private readonly ConcurrentQueue<Alert> _activeAlerts = new();
    private readonly object _lock = new();

    public event EventHandler<Alert>? AlertTriggered;

    /// <summary>
    /// Adds an alert rule.
    /// </summary>
    public void AddRule(AlertRule rule)
    {
        lock (_lock)
        {
            _rules.Add(rule);
        }
    }

    /// <summary>
    /// Processes a log entry and checks if it triggers any alerts.
    /// </summary>
    public void ProcessLogEntry(LogAnalyzer.LogEntry entry)
    {
        lock (_lock)
        {
            foreach (var rule in _rules)
            {
                if (rule.Matches(entry))
                {
                    var alert = new Alert(
                        Id: Guid.NewGuid().ToString(),
                        RuleName: rule.Name,
                        Severity: rule.Severity,
                        Message: rule.FormatMessage(entry),
                        Timestamp: DateTime.Now,
                        LogEntry: entry
                    );

                    _activeAlerts.Enqueue(alert);
                    AlertTriggered?.Invoke(this, alert);

                    Log.Warning("[ALERT] {RuleName}: {Message}", rule.Name, alert.Message);
                }
            }
        }
    }

    /// <summary>
    /// Gets all active alerts.
    /// </summary>
    public IEnumerable<Alert> GetActiveAlerts()
    {
        return _activeAlerts.ToArray();
    }

    /// <summary>
    /// Clears all active alerts.
    /// </summary>
    public void ClearAlerts()
    {
        _activeAlerts.Clear();
    }

    /// <summary>
    /// Creates default alert rules for common issues.
    /// </summary>
    public static AlertSystem CreateWithDefaultRules()
    {
        var system = new AlertSystem();

        // High error rate
        system.AddRule(new AlertRule(
            Name: "HighErrorRate",
            Severity: AlertSeverity.High,
            Condition: entry => entry.Level == "ERR" || entry.Level == "FTL",
            MessageTemplate: "Error detected: {Message}"
        ));

        // Slow performance
        system.AddRule(new AlertRule(
            Name: "SlowPerformance",
            Severity: AlertSeverity.Medium,
            Condition: entry =>
            {
                var match = System.Text.RegularExpressions.Regex.Match(entry.Message, @"in (\d+(?:\.\d+)?)ms");
                if (match.Success && double.TryParse(match.Groups[1].Value, out var ms))
                {
                    return ms > 100; // Alert if operation takes more than 100ms
                }
                return false;
            },
            MessageTemplate: "Slow operation detected: {Message}"
        ));

        // Conflict detection
        system.AddRule(new AlertRule(
            Name: "ConflictDetected",
            Severity: AlertSeverity.Medium,
            Condition: entry => entry.Message.Contains("Conflict detected", StringComparison.OrdinalIgnoreCase),
            MessageTemplate: "Action conflict: {Message}"
        ));

        // Validation failures
        system.AddRule(new AlertRule(
            Name: "ValidationFailure",
            Severity: AlertSeverity.Low,
            Condition: entry => entry.Message.Contains("validation failed", StringComparison.OrdinalIgnoreCase),
            MessageTemplate: "Validation failed: {Message}"
        ));

        // Memory issues (if logged)
        system.AddRule(new AlertRule(
            Name: "MemoryPressure",
            Severity: AlertSeverity.High,
            Condition: entry => entry.Message.Contains("OutOfMemoryException", StringComparison.OrdinalIgnoreCase),
            MessageTemplate: "Memory issue detected: {Message}"
        ));

        return system;
    }
}

/// <summary>
/// Represents an alert rule.
/// </summary>
public sealed record AlertRule(
    string Name,
    AlertSeverity Severity,
    Func<LogAnalyzer.LogEntry, bool> Condition,
    string MessageTemplate
)
{
    /// <summary>
    /// Checks if a log entry matches this rule.
    /// </summary>
    public bool Matches(LogAnalyzer.LogEntry entry)
    {
        try
        {
            return Condition(entry);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Formats the alert message.
    /// </summary>
    public string FormatMessage(LogAnalyzer.LogEntry entry)
    {
        return MessageTemplate.Replace("{Message}", entry.Message)
                              .Replace("{Level}", entry.Level)
                              .Replace("{Timestamp}", entry.Timestamp.ToString("HH:mm:ss"));
    }
}

/// <summary>
/// Represents a triggered alert.
/// </summary>
public sealed record Alert(
    string Id,
    string RuleName,
    AlertSeverity Severity,
    string Message,
    DateTime Timestamp,
    LogAnalyzer.LogEntry LogEntry
);

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Alert notification handler.
/// </summary>
public static class AlertNotifier
{
    /// <summary>
    /// Sends alert to console with colored output.
    /// </summary>
    public static void NotifyConsole(Alert alert)
    {
        var color = alert.Severity switch
        {
            AlertSeverity.Low => ConsoleColor.Yellow,
            AlertSeverity.Medium => ConsoleColor.DarkYellow,
            AlertSeverity.High => ConsoleColor.Red,
            AlertSeverity.Critical => ConsoleColor.Magenta,
            _ => ConsoleColor.White
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{alert.Severity.ToString().ToUpper()}] {alert.RuleName}: {alert.Message}");
        Console.ForegroundColor = originalColor;
    }

    /// <summary>
    /// Writes alert to a dedicated alert log file.
    /// </summary>
    public static void WriteToFile(Alert alert, string filePath)
    {
        var logEntry = $"[{alert.Timestamp:yyyy-MM-dd HH:mm:ss}] [{alert.Severity}] {alert.RuleName}: {alert.Message}";
        File.AppendAllText(filePath, logEntry + Environment.NewLine);
    }

    /// <summary>
    /// Sends alert via email (placeholder implementation).
    /// </summary>
    public static void SendEmail(Alert alert, string recipientEmail)
    {
        // In a real implementation, this would send an email
        // For now, just log it
        Log.Information("Alert email would be sent to {Email}: {Message}", recipientEmail, alert.Message);
    }

    /// <summary>
    /// Creates a desktop notification (Windows only).
    /// </summary>
    public static void ShowDesktopNotification(Alert alert)
    {
        // This would require platform-specific implementation
        // Placeholder for now
        Log.Information("Desktop notification: {Message}", alert.Message);
    }
}

/// <summary>
/// Monitors log files in real-time and triggers alerts.
/// </summary>
public sealed class RealTimeLogMonitor : IDisposable
{
    private readonly AlertSystem _alertSystem;
    private readonly FileSystemWatcher _watcher;
    private readonly string _logFilePath;
    private long _lastPosition;

    public RealTimeLogMonitor(string logFilePath, AlertSystem alertSystem)
    {
        _logFilePath = logFilePath;
        _alertSystem = alertSystem;
        _lastPosition = File.Exists(logFilePath) ? new FileInfo(logFilePath).Length : 0;

        var directory = Path.GetDirectoryName(logFilePath) ?? throw new ArgumentException("Invalid log file path");
        var fileName = Path.GetFileName(logFilePath);

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnLogFileChanged;
    }

    private void OnLogFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(_lastPosition, SeekOrigin.Begin);

            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // Parse the log line and check for alerts
                var entries = LogAnalyzer.ParseLogFile(_logFilePath).ToList();
                if (entries.Any())
                {
                    _alertSystem.ProcessLogEntry(entries.Last());
                }
            }

            _lastPosition = stream.Position;
        }
        catch (IOException)
        {
            // File might be locked, try again next time
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}

/// <summary>
/// Alert statistics and reporting.
/// </summary>
public static class AlertStatistics
{
    /// <summary>
    /// Generates a summary report of alerts.
    /// </summary>
    public static string GenerateReport(IEnumerable<Alert> alerts)
    {
        var alertList = alerts.ToList();
        if (!alertList.Any())
            return "No alerts recorded.";

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Alert Summary ===");
        report.AppendLine($"Total Alerts: {alertList.Count}");
        report.AppendLine();

        // By severity
        report.AppendLine("Alerts by Severity:");
        foreach (var group in alertList.GroupBy(a => a.Severity).OrderByDescending(g => g.Key))
        {
            report.AppendLine($"  {group.Key,-10}: {group.Count(),6}");
        }
        report.AppendLine();

        // By rule
        report.AppendLine("Alerts by Rule:");
        foreach (var group in alertList.GroupBy(a => a.RuleName).OrderByDescending(g => g.Count()))
        {
            report.AppendLine($"  {group.Key,-30}: {group.Count(),6}");
        }
        report.AppendLine();

        // Recent critical alerts
        var criticalAlerts = alertList.Where(a => a.Severity >= AlertSeverity.High).OrderByDescending(a => a.Timestamp).Take(10);
        if (criticalAlerts.Any())
        {
            report.AppendLine("Recent Critical/High Alerts:");
            foreach (var alert in criticalAlerts)
            {
                report.AppendLine($"  [{alert.Timestamp:HH:mm:ss}] {alert.RuleName}: {alert.Message}");
            }
        }

        return report.ToString();
    }
}