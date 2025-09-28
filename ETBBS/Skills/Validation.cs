namespace ETBBS;

public enum TargetingMode
{
    Any,
    EnemiesOnly,
    AlliesOnly,
    SelfOnly,
    None
}

public enum DistanceMetric
{
    Manhattan,
    Chebyshev,
    Euclidean
}

public sealed record ActionValidationConfig(
    string CasterId,
    string? TargetUnitId = null,
    Coord? TargetPos = null,
    IReadOnlyDictionary<string, string>? TeamOfUnit = null,
    TargetingMode Targeting = TargetingMode.Any,
    DistanceMetric DistanceMetric = DistanceMetric.Manhattan,
    int? RangeOverride = null,
    int CurrentTurn = 0,
    int? CooldownTurns = null,
    string MpVarKey = Keys.Mp,
    string PosVarKey = Keys.Pos,
    bool RequireMp = true
);

public interface ICooldownStore
{
    int? GetLastUseTurn(string unitId, string skillName);
    void SetLastUseTurn(string unitId, string skillName, int turn);
}

public sealed class InMemoryCooldownStore : ICooldownStore
{
    private readonly Dictionary<(string unit, string skill), int> _data = new();
    public int? GetLastUseTurn(string unitId, string skillName)
        => _data.TryGetValue((unitId, skillName), out var t) ? t : null;
    public void SetLastUseTurn(string unitId, string skillName, int turn)
        => _data[(unitId, skillName)] = turn;
}

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

    private static int Distance(Coord a, Coord b, DistanceMetric metric)
        => metric switch
        {
            DistanceMetric.Manhattan => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y),
            DistanceMetric.Chebyshev => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)),
            DistanceMetric.Euclidean => (int)Math.Round(Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y))),
            _ => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)
        };
}

