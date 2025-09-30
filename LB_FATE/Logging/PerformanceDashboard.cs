using System.Diagnostics;
using System.Collections.Concurrent;

namespace LB_FATE.Logging;

/// <summary>
/// Real-time performance monitoring dashboard for game systems.
/// </summary>
public sealed class PerformanceDashboard
{
    private readonly ConcurrentDictionary<string, MetricData> _metrics = new();
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    /// <summary>
    /// Records a metric value.
    /// </summary>
    public void Record(string metricName, double value)
    {
        _metrics.AddOrUpdate(
            metricName,
            _ => new MetricData(value),
            (_, existing) => existing.Update(value)
        );
    }

    /// <summary>
    /// Increments a counter.
    /// </summary>
    public void Increment(string counterName, int amount = 1)
    {
        _metrics.AddOrUpdate(
            counterName,
            _ => new MetricData(amount),
            (_, existing) => existing.Update(existing.Total + amount)
        );
    }

    /// <summary>
    /// Records the start of a timed operation.
    /// </summary>
    public IDisposable Time(string operationName)
    {
        return new TimedOperation(this, operationName);
    }

    /// <summary>
    /// Gets the current value of a metric.
    /// </summary>
    public MetricData? GetMetric(string name)
    {
        return _metrics.TryGetValue(name, out var data) ? data : null;
    }

    /// <summary>
    /// Gets all metrics.
    /// </summary>
    public IReadOnlyDictionary<string, MetricData> GetAllMetrics()
    {
        return _metrics;
    }

    /// <summary>
    /// Renders the dashboard to console.
    /// </summary>
    public void Render()
    {
        Console.Clear();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           LB_FATE Performance Dashboard                      ║");
        Console.WriteLine($"║ Uptime: {_uptime.Elapsed:hh\\:mm\\:ss}                                              ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");

        RenderSection("⚡ System Performance", new[]
        {
            "TurnExecutionTimeMs",
            "SkillExecutionTimeMs",
            "ActionExecutionTimeMs"
        });

        RenderSection("📊 Game Statistics", new[]
        {
            "TotalTurns",
            "TotalSkillsExecuted",
            "TotalActionsExecuted",
            "TotalDamageDealt",
            "TotalHealingDone"
        });

        RenderSection("⚔️ Combat Metrics", new[]
        {
            "CriticalHits",
            "AttacksEvaded",
            "UnitsKilled",
            "StatusEffectsApplied"
        });

        RenderSection("🔧 Engine Metrics", new[]
        {
            "ConflictsDetected",
            "ValidationFailures",
            "ExceptionsThrown"
        });

        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine("Press 'Q' to quit, 'R' to reset, any other key to refresh...");
    }

    private void RenderSection(string title, string[] metricNames)
    {
        Console.WriteLine($"║ {title,-60} ║");
        Console.WriteLine("╟──────────────────────────────────────────────────────────────╢");

        foreach (var name in metricNames)
        {
            if (_metrics.TryGetValue(name, out var data))
            {
                var displayName = name.Length > 25 ? name.Substring(0, 22) + "..." : name;
                var value = FormatValue(name, data);
                Console.WriteLine($"║  {displayName,-25} : {value,30} ║");
            }
        }

        Console.WriteLine("╟──────────────────────────────────────────────────────────────╢");
    }

    private string FormatValue(string metricName, MetricData data)
    {
        if (metricName.EndsWith("TimeMs"))
        {
            return $"{data.Average:F2}ms (min:{data.Min:F2} max:{data.Max:F2})";
        }
        else if (metricName.StartsWith("Total") || metricName.EndsWith("Count"))
        {
            return $"{data.Total:N0}";
        }
        else
        {
            return $"{data.Latest:F2} (avg:{data.Average:F2})";
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        _metrics.Clear();
        _uptime.Restart();
    }

    /// <summary>
    /// Exports metrics to JSON format.
    /// </summary>
    public string ExportToJson()
    {
        var data = _metrics.ToDictionary(
            kv => kv.Key,
            kv => new
            {
                kv.Value.Count,
                kv.Value.Total,
                kv.Value.Average,
                kv.Value.Min,
                kv.Value.Max,
                kv.Value.Latest
            }
        );

        return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private sealed class TimedOperation : IDisposable
    {
        private readonly PerformanceDashboard _dashboard;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;

        public TimedOperation(PerformanceDashboard dashboard, string operationName)
        {
            _dashboard = dashboard;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _dashboard.Record(_operationName, _stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}

/// <summary>
/// Statistical data for a metric.
/// </summary>
public sealed class MetricData
{
    public int Count { get; private set; }
    public double Total { get; private set; }
    public double Average => Count > 0 ? Total / Count : 0;
    public double Min { get; private set; }
    public double Max { get; private set; }
    public double Latest { get; private set; }

    public MetricData(double initialValue)
    {
        Count = 1;
        Total = initialValue;
        Min = initialValue;
        Max = initialValue;
        Latest = initialValue;
    }

    public MetricData Update(double value)
    {
        return new MetricData(this, value);
    }

    private MetricData(MetricData existing, double newValue)
    {
        Count = existing.Count + 1;
        Total = existing.Total + newValue;
        Min = Math.Min(existing.Min, newValue);
        Max = Math.Max(existing.Max, newValue);
        Latest = newValue;
    }
}

/// <summary>
/// Interactive dashboard runner.
/// </summary>
public static class DashboardRunner
{
    private static PerformanceDashboard? _dashboard;
    private static Thread? _renderThread;
    private static bool _running;

    /// <summary>
    /// Starts the dashboard in a background thread.
    /// </summary>
    public static PerformanceDashboard Start()
    {
        if (_dashboard != null)
            return _dashboard;

        _dashboard = new PerformanceDashboard();
        _running = true;

        _renderThread = new Thread(() =>
        {
            while (_running)
            {
                try
                {
                    _dashboard.Render();
                    Thread.Sleep(1000); // Update every second
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Dashboard error: {ex.Message}");
                }
            }
        })
        {
            IsBackground = true,
            Name = "PerformanceDashboard"
        };

        _renderThread.Start();
        return _dashboard;
    }

    /// <summary>
    /// Stops the dashboard.
    /// </summary>
    public static void Stop()
    {
        _running = false;
        _renderThread?.Join(2000);
        _dashboard = null;
    }

    /// <summary>
    /// Gets the current dashboard instance.
    /// </summary>
    public static PerformanceDashboard? Instance => _dashboard;
}