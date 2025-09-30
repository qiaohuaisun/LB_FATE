namespace ETBBS;

/// <summary>
/// Targeting restrictions for skills.
/// </summary>
public enum TargetingMode
{
    /// <summary>Any unit can be targeted.</summary>
    Any,
    /// <summary>Only enemy units can be targeted.</summary>
    EnemiesOnly,
    /// <summary>Only ally units can be targeted.</summary>
    AlliesOnly,
    /// <summary>Only the caster can target themselves.</summary>
    SelfOnly,
    /// <summary>No unit targeting required.</summary>
    None
}

/// <summary>
/// Distance calculation methods for range validation.
/// </summary>
public enum DistanceMetric
{
    /// <summary>Grid-based distance (|dx| + |dy|).</summary>
    Manhattan,
    /// <summary>Chessboard distance (max(|dx|, |dy|)).</summary>
    Chebyshev,
    /// <summary>Straight-line distance (sqrt(dx² + dy²)).</summary>
    Euclidean
}

/// <summary>
/// Configuration for validating skill/action execution.
/// Contains all context needed for range, team, MP, and cooldown checks.
/// </summary>
public sealed record ActionValidationConfig(
    string CasterId,
    string? TargetUnitId = null,
    Coord? TargetPos = null,
    IReadOnlyDictionary<string, string>? TeamOfUnit = null,
    TargetingMode Targeting = TargetingMode.Any,
    DistanceMetric DistanceMetric = DistanceMetric.Manhattan,
    int? RangeOverride = null,
    int CurrentTurn = 0,
    int CurrentDay = 0,
    int CurrentPhase = 0,
    int? CooldownTurns = null,
    string MpVarKey = Keys.Mp,
    string PosVarKey = Keys.Pos,
    bool RequireMp = true
);

/// <summary>
/// Storage interface for skill cooldown tracking.
/// Implementations can be in-memory, persistent, or distributed.
/// </summary>
public interface ICooldownStore
{
    /// <summary>
    /// Gets the last turn when a unit used a specific skill.
    /// </summary>
    /// <returns>The turn number, or null if never used.</returns>
    int? GetLastUseTurn(string unitId, string skillName);

    /// <summary>
    /// Records that a unit used a skill on the specified turn.
    /// </summary>
    void SetLastUseTurn(string unitId, string skillName, int turn);
}

/// <summary>
/// Simple in-memory cooldown storage.
/// Suitable for single-game sessions; data is not persisted.
/// </summary>
public sealed class InMemoryCooldownStore : ICooldownStore
{
    private readonly Dictionary<(string unit, string skill), int> _data = new();

    /// <inheritdoc />
    public int? GetLastUseTurn(string unitId, string skillName)
        => _data.TryGetValue((unitId, skillName), out var t) ? t : null;

    /// <inheritdoc />
    public void SetLastUseTurn(string unitId, string skillName, int turn)
        => _data[(unitId, skillName)] = turn;
}

/// <summary>
/// Factory for creating action validators with common game rules.
/// Provides composable validation logic for MP, range, team, cooldown, etc.
/// </summary>
public static class ActionValidators
{
    public static ActionValidator Compose(params ActionValidator[] validators)
        => (Context ctx, AtomicAction[] actions, out string? reason) =>
        {
            foreach (var v in validators)
            {
                if (!v(ctx, actions, out reason))
                    return false;
            }
            reason = null;
            return true;
        };

    public static ActionValidator ForSkill(Skill skill, ActionValidationConfig cfg, ICooldownStore? cooldownStore = null)
    {
        var list = new List<ActionValidator>();

        // MP check
        if (cfg.RequireMp && skill.Metadata.MpCost > 0)
            list.Add(CreateMpValidator(cfg, skill.Metadata.MpCost));

        // Range check
        var range = cfg.RangeOverride ?? skill.Metadata.Range;
        if (range > 0)
            list.Add(CreateRangeValidator(cfg, range));

        // Team rule
        if (cfg.Targeting != TargetingMode.Any && cfg.Targeting != TargetingMode.None)
            list.Add(CreateTeamValidator(cfg));

        // Target must be targetable (generic)
        list.Add(CreateTargetableValidator(cfg));

        // Cooldown
        if (cfg.CooldownTurns is int cd && cd > 0 && cooldownStore is not null)
            list.Add(CreateCooldownValidator(skill, cfg, cooldownStore));

        if (list.Count == 0)
            return AllowAll;

        return Compose(list.ToArray());
    }

    // Helper: read extras like cooldown/targeting and apply to base cfg
    public static ActionValidator ForSkillWithExtras(Skill skill, ActionValidationConfig baseCfg, ICooldownStore? cooldownStore = null)
    {
        var cfg = baseCfg;
        if (skill.Extras.TryGetValue("targeting", out var tval) && tval is string ts)
        {
            cfg = cfg with
            {
                Targeting = ts.ToLowerInvariant() switch
                {
                    "any" => TargetingMode.Any,
                    "enemies" => TargetingMode.EnemiesOnly,
                    "allies" => TargetingMode.AlliesOnly,
                    "self" => TargetingMode.SelfOnly,
                    _ => cfg.Targeting
                }
            };
        }
        if (skill.Extras.TryGetValue("cooldown", out var cdObj) && cdObj is int cd && cooldownStore is not null)
        {
            cfg = cfg with { CooldownTurns = cd };
        }
        var baseValidator = ForSkill(skill, cfg, cooldownStore);
        // min_range: target must be at least this far
        if (skill.Extras.TryGetValue("min_range", out var mrObj) && mrObj is int minr && minr > 0)
        {
            baseValidator = Compose(baseValidator, CreateMinRangeValidator(cfg, minr));
        }
        // sealed_until: skill unusable before specified turn
        if (skill.Extras.TryGetValue("sealed_until", out var suObj) && suObj is int sealedUntil)
        {
            return Compose(baseValidator, CreateSealedUntilValidator(sealedUntil, cfg));
        }
        // sealed_until_day[/phase]
        if (skill.Extras.TryGetValue("sealed_until_day", out var sdObj) && sdObj is int sealedDay)
        {
            int? sealedPhase = null;
            if (skill.Extras.TryGetValue("sealed_until_phase", out var spObj) && spObj is int sp)
                sealedPhase = sp;
            return Compose(baseValidator, CreateSealedUntilDayPhaseValidator(sealedDay, sealedPhase, cfg));
        }
        return baseValidator;
    }

    public static ActionValidator AllowAll => (Context ctx, AtomicAction[] actions, out string? reason) => { reason = null; return true; };

    public static ActionValidator CreateMpValidator(ActionValidationConfig cfg, int requiredMp)
        => (Context ctx, AtomicAction[] actions, out string? reason) =>
        {
            var obj = ctx.GetUnitVar<object>(cfg.CasterId, cfg.MpVarKey, 0);
            double mp = obj switch { int i => i, double d => d, float f => f, long l => l, _ => 0.0 };
            if (mp < requiredMp)
            {
                reason = $"Not enough MP: {mp}/{requiredMp}";
                return false;
            }
            reason = null;
            return true;
        };

    public static ActionValidator CreateMinRangeValidator(ActionValidationConfig cfg, int minRange)
        => (Context ctx, AtomicAction[] actions, out string? reason) =>
        {
            var casterPos = ctx.GetUnitVar<Coord>(cfg.CasterId, cfg.PosVarKey, default);
            if (casterPos.Equals(default(Coord)))
            {
                reason = "Caster position missing";
                return false;
            }

            Coord? targetPos = cfg.TargetPos;
            if (targetPos is null && cfg.TargetUnitId is not null)
            {
                var pos = ctx.GetUnitVar<Coord>(cfg.TargetUnitId, cfg.PosVarKey, default);
                if (!pos.Equals(default(Coord))) targetPos = pos;
            }

            // Non-positional skill: skip
            if (targetPos is null)
            {
                reason = null;
                return true;
            }

            var dist = Distance(casterPos, targetPos.Value, cfg.DistanceMetric);
            if (dist < minRange)
            {
                reason = $"Below min range: d={dist}, min_range={minRange}";
                return false;
            }

            reason = null;
            return true;
        };

    public static ActionValidator CreateRangeValidator(ActionValidationConfig cfg, int range)
        => (Context ctx, AtomicAction[] actions, out string? reason) =>
        {
            // Resolve positions
            var casterPos = ctx.GetUnitVar<Coord>(cfg.CasterId, cfg.PosVarKey, default);
            if (casterPos.Equals(default(Coord)))
            {
                reason = "Caster position missing";
                return false;
            }

            Coord? targetPos = cfg.TargetPos;
            if (targetPos is null && cfg.TargetUnitId is not null)
            {
                var pos = ctx.GetUnitVar<Coord>(cfg.TargetUnitId, cfg.PosVarKey, default);
                if (!pos.Equals(default(Coord))) targetPos = pos;
            }

            // If no target position is known, skip range check (non-positional skill)
            if (targetPos is null)
            {
                reason = null;
                return true;
            }

            var dist = Distance(casterPos, targetPos.Value, cfg.DistanceMetric);
            if (dist > range)
            {
                reason = $"Out of range: d={dist}, range={range}";
                return false;
            }

            reason = null;
            return true;
        };

    public static ActionValidator CreateTeamValidator(ActionValidationConfig cfg)
        => (Context ctx, AtomicAction[] actions, out string? reason) =>
        {
            if (cfg.TargetUnitId is null || cfg.TeamOfUnit is null)
            {
                reason = null; // nothing to validate
                return true;
            }

            var casterTeam = cfg.TeamOfUnit.TryGetValue(cfg.CasterId, out var ct) ? ct : null;
            var targetTeam = cfg.TeamOfUnit.TryGetValue(cfg.TargetUnitId, out var tt) ? tt : null;

            switch (cfg.Targeting)
            {
                case TargetingMode.SelfOnly:
                    if (cfg.TargetUnitId != cfg.CasterId)
                    { reason = "Target must be self"; return false; }
                    break;
                case TargetingMode.AlliesOnly:
                    if (casterTeam is null || targetTeam is null || casterTeam != targetTeam)
                    { reason = "Target must be an ally"; return false; }
                    break;
                case TargetingMode.EnemiesOnly:
                    if (casterTeam is null || targetTeam is null || casterTeam == targetTeam)
                    { reason = "Target must be an enemy"; return false; }
                    break;
            }

            reason = null;
            return true;
        };

    public static ActionValidator CreateTargetableValidator(ActionValidationConfig cfg)
        => (Context ctx, AtomicAction[] actions, out string? reason) =>
        {
            if (cfg.TargetUnitId is null)
            {
                reason = null; return true;
            }
            var tpos = ctx.GetUnitVar<object>(cfg.TargetUnitId, Keys.Pos, default(Coord)); // touch to ensure unit exists
            // Untargetable while turns > 0
            var turns = ctx.GetUnitVar<int>(cfg.TargetUnitId, Keys.UntargetableTurns, 0);
            if (turns > 0)
            {
                reason = "Target is untargetable"; return false;
            }
            reason = null; return true;
        };

    public static ActionValidator CreateCooldownValidator(Skill skill, ActionValidationConfig cfg, ICooldownStore store)
        => (Context ctx, AtomicAction[] actions, out string? reason) =>
        {
            var last = store.GetLastUseTurn(cfg.CasterId, skill.Metadata.Name);
            if (last is int t && t + (cfg.CooldownTurns ?? 0) > cfg.CurrentTurn)
            {
                var ready = t + (cfg.CooldownTurns ?? 0);
                reason = $"On cooldown until turn {ready}";
                return false;
            }
            reason = null;
            return true;
        };

    public static ActionValidator CreateSealedUntilValidator(int sealedUntilTurn, ActionValidationConfig cfg)
        => (Context ctx, AtomicAction[] actions, out string? reason) =>
        {
            if (cfg.CurrentTurn < sealedUntilTurn)
            {
                reason = $"Sealed until turn {sealedUntilTurn}";
                return false;
            }
            reason = null;
            return true;
        };

    public static ActionValidator CreateSealedUntilDayPhaseValidator(int sealedUntilDay, int? sealedUntilPhase, ActionValidationConfig cfg)
        => (Context ctx, AtomicAction[] actions, out string? reason) =>
        {
            // Day/Phase are 1-based in UX; cfg carries exact current values from host game.
            int curDay = cfg.CurrentDay;
            int curPhase = cfg.CurrentPhase;
            int reqDay = Math.Max(1, sealedUntilDay);
            int reqPhase = Math.Max(1, sealedUntilPhase ?? 1); // default unlock at day start

            bool locked = (curDay < reqDay) || (curDay == reqDay && curPhase < reqPhase);
            if (locked)
            {
                reason = $"Sealed until day {reqDay} phase {reqPhase}";
                return false;
            }
            reason = null;
            return true;
        };

    private static int Distance(Coord a, Coord b, DistanceMetric metric)
        => metric switch
        {
            DistanceMetric.Manhattan => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y),
            DistanceMetric.Chebyshev => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)),
            DistanceMetric.Euclidean => (int)Math.Round(Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y))),
            _ => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)
        };
}

