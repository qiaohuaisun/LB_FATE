using System;
using System.IO;
using System.Linq;
using Xunit;

public class LogAnalyzerTests
{
    [Fact]
    public void Parse_Filter_Search_Stats_Perf_Work()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"lb_fate_log_{Guid.NewGuid():N}.log");
        try
        {
            File.WriteAllLines(tmp, new[]
            {
                "2025-10-01 12:00:00.123 +08:00 [INF] Startup done",
                "2025-10-01 12:00:01.000 +08:00 [ERR] Something failed",
                "System.Exception: oops",
                "   at Foo.Bar()",
                "2025-10-01 12:00:02.000 +08:00 [INF] Turn executed in 123.45ms"
            });

            var entries = LB_FATE.Logging.LogAnalyzer.ParseLogFile(tmp).ToList();
            Assert.Equal(3, entries.Count);
            Assert.Contains(entries, e => e.Level == "ERR");
            Assert.Contains(entries, e => e.Message.Contains("Startup"));
            Assert.Contains(entries, e => e.Message.Contains("Turn executed"));
            Assert.Contains(entries, e => !string.IsNullOrWhiteSpace(e.Exception));

            var errs = LB_FATE.Logging.LogAnalyzer.FilterByLevel(entries, "ERR").ToList();
            Assert.Single(errs);

            var search = LB_FATE.Logging.LogAnalyzer.Search(entries, "Startup").ToList();
            Assert.Single(search);

            var stats = LB_FATE.Logging.LogAnalyzer.GenerateStatistics(entries);
            Assert.Equal(3, stats.TotalEntries);
            Assert.True(stats.LevelCounts.ContainsKey("INF"));
            Assert.Equal(1, stats.ExceptionCount);

            var metrics = LB_FATE.Logging.LogAnalyzer.ExtractPerformanceMetrics(entries).ToList();
            Assert.Single(metrics);
            Assert.Equal("Turn executed", metrics[0].OperationName);
            Assert.True(metrics[0].DurationMs > 100);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }
}
