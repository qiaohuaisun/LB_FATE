using ETBBS;
using Microsoft.Extensions.Logging;
using Xunit;

public class ActionLoggingTests
{
    public ActionLoggingTests()
    {
        // Ensure ETBBS logging is configured to a no-op logger to avoid noisy output
        ETBBSLog.Configure(LoggerFactory.Create(builder => { }));
    }

    [Fact]
    public void LogDamage_Covers_All_Branches()
    {
        // evaded branch
        ActionLogging.LogDamage("U1", 10, 0, 50, 50, evaded: true);
        // shield absorbed branch
        ActionLogging.LogDamage("U1", 10, 0, 50, 50, shieldAbsorbed: true);
        // normal hit + lethal info branch
        ActionLogging.LogDamage("U1", 10, 10, 5, 0);
    }

    [Fact]
    public void LogHeal_Covers_All_Branches()
    {
        ActionLogging.LogHeal("U1", 10, 0, 50, 40, reversed: true);
        ActionLogging.LogHeal("U1", 10, 0, 50, 50, blocked: true);
        ActionLogging.LogHeal("U1", 10, 10, 40, 50);
    }

    [Fact]
    public void LogMove_And_Status_Tag_Var_AoE_Others_Work()
    {
        var from = new Coord(1, 2);
        var to = new Coord(3, 5);
        ActionLogging.LogMove("U1", from, to);

        ActionLogging.LogStatusEffect("U1", "stun", 2, applied: true);
        ActionLogging.LogStatusEffect("U1", "stun", 2, applied: false);

        ActionLogging.LogTagChange("U1", "flying", added: true);
        ActionLogging.LogTagChange("U1", "flying", added: false);

        ActionLogging.LogVarChange("U1", "hp", 10, 5);
        ActionLogging.LogVarChange("U1", "hp", null, null);

        ActionLogging.LogAoE("U1", "Fireball", 3, new Coord(2, 2), 2);
        ActionLogging.LogLineAoE("U1", "Slash", 2, from, to, 3, 1);

        ActionLogging.LogCriticalHit("U1", "U2", 15, 2, 1.5);
        ActionLogging.LogEvasion("U1", "U2", 0.25);
        ActionLogging.LogShieldAbsorb("U1", 10, 5.0, 0.0, 2);
        ActionLogging.LogUndyingActivation("U1", 999, 1);
        ActionLogging.LogMpConsumption("U1", 10.0, 7.5, 2.5);
        ActionLogging.LogCooldownSet("U1", "Fireball", 2);
    }
}

