using ETBBS;
using Microsoft.Extensions.Logging;
using Xunit;

public class LoggingHelpersTests
{
    private readonly ILogger _logger;

    public LoggingHelpersTests()
    {
        _logger = LoggerFactory.Create(b => { }).CreateLogger("ETBBS.Tests");
    }

    [Fact]
    public void ActionExecutionLogger_Start_Complete_Fail_DoNotThrow()
    {
        var a = ActionExecutionLogger.Start(_logger, "TestAction");
        a.Complete();

        var b = ActionExecutionLogger.Start(_logger, "TestAction");
        b.Fail(new System.Exception("boom"));
    }

    [Fact]
    public void SkillExecutionLogger_Start_Complete_Validation_Fail_DoNotThrow()
    {
        var s = SkillExecutionLogger.Start(_logger, "Fireball", "U1");
        s.ValidationFailed("no target");
        s.Complete(3, 2);

        var s2 = SkillExecutionLogger.Start(_logger, "Fireball", "U1");
        s2.Fail(new System.InvalidOperationException("fail"));
    }
}

