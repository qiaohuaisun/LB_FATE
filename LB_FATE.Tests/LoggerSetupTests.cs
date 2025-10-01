using System;
using System.IO;
using Serilog;
using Serilog.Events;
using Xunit;

[Collection("LoggerSeq")]
public class LoggerSetupTests
{
    [Fact]
    public void CreateLogger_Writes_To_File_When_Configured()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"lb_fate_test_{Guid.NewGuid():N}.log");
        try
        {
            var logger = LB_FATE.Logging.LoggerSetup.CreateLogger(LogEventLevel.Debug, tmp, enablePerformanceMetrics: true);
            Assert.NotNull(logger);
            logger.Information("hello world");
            Log.CloseAndFlush();

            // Give a moment for file write
            System.Threading.Thread.Sleep(100);

            var dir = Path.GetDirectoryName(tmp)!;
            var prefix = Path.GetFileNameWithoutExtension(tmp);
            var candidates = Directory.EnumerateFiles(dir, prefix + "*.log");
            Assert.Contains(candidates, f => new FileInfo(f).Length >= 0);
        }
        finally
        {
            try
            {
                var dir = Path.GetDirectoryName(tmp)!;
                var prefix = Path.GetFileNameWithoutExtension(tmp);
                foreach (var f in Directory.EnumerateFiles(dir, prefix + "*.log"))
                    File.Delete(f);
            }
            catch { }
        }
    }

    [Fact]
    public void Initialize_And_Close_GlobalLogger_Do_NotThrow()
    {
        LB_FATE.Logging.LoggerSetup.InitializeGlobalLogger(enableFileLogging: false, enablePerformanceMetrics: false, minLevel: LogEventLevel.Information);
        Log.Information("test message");
        LB_FATE.Logging.LoggerSetup.CloseLogger();
    }
}
