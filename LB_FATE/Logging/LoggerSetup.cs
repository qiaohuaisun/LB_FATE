using ETBBS;
using Serilog;
using Serilog.Events;

namespace LB_FATE.Logging;

/// <summary>
/// Provides centralized logging configuration for the application.
/// Integrates Serilog with Microsoft.Extensions.Logging for both LB_FATE and ETBBS.
/// </summary>
public static class LoggerSetup
{
    /// <summary>
    /// Configures and returns a Serilog logger instance.
    /// </summary>
    /// <param name="logLevel">The minimum log level to capture.</param>
    /// <param name="logFilePath">Optional path to a log file. If null, file logging is disabled.</param>
    /// <param name="enablePerformanceMetrics">If true, enables detailed performance tracking.</param>
    /// <returns>A configured ILogger instance.</returns>
    public static Serilog.ILogger CreateLogger(LogEventLevel logLevel = LogEventLevel.Information, string? logFilePath = null, bool enablePerformanceMetrics = false)
    {
        var config = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .Enrich.WithProperty("Application", "LB_FATE")
            .Enrich.WithThreadId()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: logLevel
            );

        if (!string.IsNullOrWhiteSpace(logFilePath))
        {
            config = config.WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: logLevel
            );
        }

        // Add performance metrics sink if enabled
        if (enablePerformanceMetrics)
        {
            var perfLogPath = logFilePath?.Replace(".log", "_perf.log")
                ?? Path.Combine(AppContext.BaseDirectory, "logs", "performance_.log");

            config = config.WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e =>
                    e.Properties.ContainsKey("ElapsedMs") ||
                    e.Properties.ContainsKey("ActionName") ||
                    e.Properties.ContainsKey("SkillName"))
                .WriteTo.File(
                    perfLogPath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties}{NewLine}")
            );
        }

        return config.CreateLogger();
    }

    /// <summary>
    /// Sets up the global logger for the application and configures ETBBS logging.
    /// </summary>
    /// <param name="enableFileLogging">If true, logs will be written to files in the logs directory.</param>
    /// <param name="enablePerformanceMetrics">If true, enables detailed performance tracking.</param>
    /// <param name="minLevel">Minimum log level (default: Information for production, Debug for development).</param>
    public static void InitializeGlobalLogger(bool enableFileLogging = true, bool enablePerformanceMetrics = false, LogEventLevel minLevel = LogEventLevel.Information)
    {
        var logPath = enableFileLogging
            ? Path.Combine(AppContext.BaseDirectory, "logs", "lb_fate_.log")
            : null;

        Log.Logger = CreateLogger(minLevel, logPath, enablePerformanceMetrics);

        // Configure ETBBS engine logging
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.Logger);
        });
        ETBBSLog.Configure(loggerFactory);

        Log.Information("Logger initialized (FileLogging: {FileLogging}, PerfMetrics: {PerfMetrics})",
            enableFileLogging, enablePerformanceMetrics);
    }

    /// <summary>
    /// Closes and flushes the global logger.
    /// Should be called before application exit.
    /// </summary>
    public static void CloseLogger()
    {
        Log.Information("Shutting down logger");
        Log.CloseAndFlush();
    }
}