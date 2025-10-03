using ETBBS;
using Xunit;

public class ActionMetricsTests
{
    [Fact]
    public void Record_And_Report_Works()
    {
        var m = new ActionMetrics();

        // Fast actions
        m.Record("Attack", 1.2);
        m.Record("Attack", 2.3);
        m.Record("Heal", 0.7);

        // Slow action (trigger warning path)
        m.Record("Cast", 12.5);

        var report = m.GenerateReport();
        Assert.False(string.IsNullOrWhiteSpace(report));
        Assert.Contains("Action Performance Metrics", report);
        Assert.Contains("Attack", report);
        Assert.Contains("Heal", report);
        Assert.Contains("Cast", report);

        // Reset clears metrics
        m.Reset();
        var empty = m.GenerateReport();
        // Still has header but no entries appended
        Assert.Contains("Action Performance Metrics", empty);
    }
}

