using Microsoft.Extensions.Logging;

namespace ETBBS;

/// <summary>
/// Provides logging decorators for atomic actions to enable detailed execution tracing.
/// </summary>
public static class ActionLogging
{
    private static readonly ILogger _logger = ETBBSLog.CreateLogger("ETBBS.Actions");

    /// <summary>
    /// Logs the execution of a Damage action with detailed information.
    /// </summary>
    public static void LogDamage(string targetId, int amount, int actualDamage, int hpBefore, int hpAfter, bool evaded = false, bool shieldAbsorbed = false)
    {
        if (evaded)
        {
            _logger.LogDebug("Damage evaded: Target={Target}, Amount={Amount}", targetId, amount);
        }
        else if (shieldAbsorbed)
        {
            _logger.LogDebug("Damage absorbed by shield: Target={Target}, Amount={Amount}, Actual={Actual}, HP={HpBefore}->{HpAfter}",
                targetId, amount, actualDamage, hpBefore, hpAfter);
        }
        else
        {
            _logger.LogDebug("Damage dealt: Target={Target}, Amount={Amount}, HP={HpBefore}->{HpAfter}",
                targetId, actualDamage, hpBefore, hpAfter);

            // Warning for lethal damage
            if (hpAfter <= 0 && hpBefore > 0)
            {
                _logger.LogInformation("Unit killed by damage: {Target}, FinalDamage={Damage}", targetId, actualDamage);
            }
        }
    }

    /// <summary>
    /// Logs the execution of a Heal action.
    /// </summary>
    public static void LogHeal(string targetId, int amount, int actualHeal, int hpBefore, int hpAfter, bool reversed = false, bool blocked = false)
    {
        if (reversed)
        {
            _logger.LogDebug("Heal reversed to damage: Target={Target}, Amount={Amount}", targetId, amount);
        }
        else if (blocked)
        {
            _logger.LogDebug("Heal blocked: Target={Target}, Amount={Amount}", targetId, amount);
        }
        else
        {
            _logger.LogDebug("Heal applied: Target={Target}, Amount={Amount}, HP={HpBefore}->{HpAfter}",
                targetId, actualHeal, hpBefore, hpAfter);
        }
    }

    /// <summary>
    /// Logs a unit movement action.
    /// </summary>
    public static void LogMove(string unitId, Coord from, Coord to)
    {
        var distance = Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
        _logger.LogDebug("Unit moved: {UnitId} from {From} to {To} (Distance={Distance})",
            unitId, from, to, distance);
    }

    /// <summary>
    /// Logs a status effect application.
    /// </summary>
    public static void LogStatusEffect(string targetId, string effectType, int duration, bool applied)
    {
        if (applied)
        {
            _logger.LogDebug("Status effect applied: {Effect} on {Target} for {Duration} turns",
                effectType, targetId, duration);
        }
        else
        {
            _logger.LogDebug("Status effect blocked: {Effect} on {Target} (immune or already affected)",
                effectType, targetId);
        }
    }

    /// <summary>
    /// Logs a tag modification.
    /// </summary>
    public static void LogTagChange(string targetId, string tag, bool added)
    {
        if (added)
        {
            _logger.LogTrace("Tag added: {Tag} to {Target}", tag, targetId);
        }
        else
        {
            _logger.LogTrace("Tag removed: {Tag} from {Target}", tag, targetId);
        }
    }

    /// <summary>
    /// Logs a variable modification.
    /// </summary>
    public static void LogVarChange(string targetId, string varName, object? oldValue, object? newValue)
    {
        _logger.LogTrace("Variable changed: {Target}.{VarName} = {OldValue} -> {NewValue}",
            targetId, varName, oldValue ?? "null", newValue ?? "null");
    }

    /// <summary>
    /// Logs an AoE effect.
    /// </summary>
    public static void LogAoE(string casterId, string skillName, int targetsHit, Coord center, int radius)
    {
        _logger.LogInformation("AoE skill executed: {Skill} by {Caster} at {Center} (Radius={Radius}, Targets={Targets})",
            skillName, casterId, center, radius, targetsHit);
    }

    /// <summary>
    /// Logs a line AoE effect.
    /// </summary>
    public static void LogLineAoE(string casterId, string skillName, int targetsHit, Coord from, Coord to, int length, int width)
    {
        _logger.LogInformation("Line AoE skill executed: {Skill} by {Caster} from {From} to {To} (Length={Length}, Width={Width}, Targets={Targets})",
            skillName, casterId, from, to, length, width, targetsHit);
    }

    /// <summary>
    /// Logs a critical hit.
    /// </summary>
    public static void LogCriticalHit(string attackerId, string targetId, int normalDamage, int critDamage, double critMultiplier)
    {
        _logger.LogInformation("Critical hit! {Attacker} -> {Target}: {Normal}dmg x{Multiplier} = {Crit}dmg",
            attackerId, targetId, normalDamage, critMultiplier, critDamage);
    }

    /// <summary>
    /// Logs a dodge/evasion.
    /// </summary>
    public static void LogEvasion(string attackerId, string defenderId, double evasionChance)
    {
        _logger.LogInformation("Attack evaded: {Attacker} -> {Defender} (EvasionRate={Rate:P0})",
            attackerId, defenderId, evasionChance);
    }

    /// <summary>
    /// Logs shield absorption.
    /// </summary>
    public static void LogShieldAbsorb(string targetId, int damage, double shieldBefore, double shieldAfter, int hpDamage)
    {
        _logger.LogDebug("Shield absorbed damage: {Target} Shield={ShieldBefore}->{ShieldAfter}, Overflow={HpDamage}",
            targetId, shieldBefore, shieldAfter, hpDamage);
    }

    /// <summary>
    /// Logs undying effect activation.
    /// </summary>
    public static void LogUndyingActivation(string targetId, int lethalDamage, int turnsRemaining)
    {
        _logger.LogInformation("Undying effect activated: {Target} survived {Damage} lethal damage ({Turns} turns remaining)",
            targetId, lethalDamage, turnsRemaining);
    }

    /// <summary>
    /// Logs MP consumption.
    /// </summary>
    public static void LogMpConsumption(string unitId, double mpBefore, double mpAfter, double cost)
    {
        _logger.LogDebug("MP consumed: {Unit} MP={MpBefore}->{MpAfter} (Cost={Cost})",
            unitId, mpBefore, mpAfter, cost);
    }

    /// <summary>
    /// Logs cooldown activation.
    /// </summary>
    public static void LogCooldownSet(string unitId, string skillName, int turns)
    {
        _logger.LogDebug("Cooldown set: {Unit}.{Skill} = {Turns} turns", unitId, skillName, turns);
    }
}

/// <summary>
/// Provides performance metrics for action execution.
/// </summary>
public sealed class ActionMetrics
{
    private static readonly ILogger _logger = ETBBSLog.CreateLogger("ETBBS.Metrics");

    private readonly Dictionary<string, (int count, double totalMs, double minMs, double maxMs)> _metrics = new();
    private readonly object _lock = new();

    /// <summary>
    /// Records the execution time of an action.
    /// </summary>
    public void Record(string actionType, double elapsedMs)
    {
        lock (_lock)
        {
            if (_metrics.TryGetValue(actionType, out var existing))
            {
                _metrics[actionType] = (
                    existing.count + 1,
                    existing.totalMs + elapsedMs,
                    Math.Min(existing.minMs, elapsedMs),
                    Math.Max(existing.maxMs, elapsedMs)
                );
            }
            else
            {
                _metrics[actionType] = (1, elapsedMs, elapsedMs, elapsedMs);
            }

            // Log slow actions
            if (elapsedMs > 10.0) // 10ms threshold
            {
                _logger.LogWarning("Slow action detected: {ActionType} took {ElapsedMs}ms", actionType, elapsedMs);
            }
        }
    }

    /// <summary>
    /// Generates a summary report of action metrics.
    /// </summary>
    public string GenerateReport()
    {
        lock (_lock)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Action Performance Metrics ===");
            report.AppendLine($"{"Action Type",-30} {"Count",8} {"Avg",10} {"Min",10} {"Max",10} {"Total",12}");
            report.AppendLine(new string('-', 90));

            foreach (var (actionType, (count, totalMs, minMs, maxMs)) in _metrics.OrderByDescending(kv => kv.Value.totalMs))
            {
                var avgMs = totalMs / count;
                report.AppendLine($"{actionType,-30} {count,8} {avgMs,10:F3}ms {minMs,10:F3}ms {maxMs,10:F3}ms {totalMs,12:F2}ms");
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// Logs the current metrics summary.
    /// </summary>
    public void LogSummary()
    {
        _logger.LogInformation(GenerateReport());
    }

    /// <summary>
    /// Clears all recorded metrics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _metrics.Clear();
        }
    }
}