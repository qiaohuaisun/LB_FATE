namespace ETBBS;

public record SkillMetadata(string Name, int MpCost, int Range);

public delegate AtomicAction[] SkillEffect(Context ctx);
public delegate IReadOnlyList<AtomicAction[]> SkillPlan(Context ctx);

public sealed class Skill
{
    public SkillMetadata Metadata { get; }
    private readonly SkillPlan _plan;
    public IReadOnlyDictionary<string, object> Extras { get; }

    public Skill(SkillMetadata meta, SkillPlan plan, IReadOnlyDictionary<string, object>? extras = null)
    {
        Metadata = meta;
        _plan = plan;
        Extras = extras ?? new Dictionary<string, object>();
    }

    public AtomicAction[] BuildActions(Context ctx)
        => _plan(ctx).SelectMany(batch => batch).ToArray();

    public IReadOnlyList<AtomicAction[]> BuildPlan(Context ctx) => _plan(ctx);
}

public sealed class SkillBuilder
{
    private string _name = "Unnamed";
    private int _mp = 0;
    private int _range = 1;
    private SkillPlan _plan = ctx => Array.Empty<AtomicAction[]>() as IReadOnlyList<AtomicAction[]>;
    private readonly Dictionary<string, object> _extras = new();

    private SkillBuilder(string name) => _name = name;

    public static SkillBuilder Create(string name) => new(name);

    public SkillBuilder Cost(int mp)
    {
        _mp = Math.Max(0, mp);
        return this;
    }

    public SkillBuilder Range(int range)
    {
        _range = Math.Max(0, range);
        return this;
    }

    public SkillBuilder WithExtra(string key, object value)
    {
        _extras[key] = value;
        return this;
    }

    // Back-compat: simple effect -> single-step plan
    public SkillBuilder Effect(SkillEffect effect)
    {
        _plan = ctx => new[] { effect(ctx) };
        return this;
    }

    // New: script-style composition DSL
    public SkillBuilder Script(Action<SkillScript> build)
    {
        var script = new SkillScript();
        build(script);
        _plan = ctx => script.BuildPlan(ctx);
        return this;
    }

    public Skill Build()
    {
        var meta = new SkillMetadata(_name, _mp, _range);
        return new Skill(meta, _plan, _extras);
    }
}

public sealed class SkillScript
{
    private readonly List<Func<Context, List<AtomicAction[]>>> _steps = new();

    public SkillScript Do(params AtomicAction[] actions)
    {
        _steps.Add(ctx => new List<AtomicAction[]> { actions });
        return this;
    }

    public SkillScript Do(Func<Context, AtomicAction> action)
    {
        _steps.Add(ctx => new List<AtomicAction[]> { new[] { action(ctx) } });
        return this;
    }

    public SkillScript Do(Func<Context, AtomicAction[]> actions)
    {
        _steps.Add(ctx => new List<AtomicAction[]> { actions(ctx) });
        return this;
    }

    public SkillScript Parallel(params Action<SkillScript>[] branches)
    {
        _steps.Add(ctx =>
        {
            var merged = new List<AtomicAction>();
            foreach (var b in branches)
            {
                var sub = new SkillScript();
                b(sub);
                var plan = sub.BuildPlan(ctx);
                foreach (var batch in plan)
                    merged.AddRange(batch);
            }
            return new List<AtomicAction[]> { merged.ToArray() };
        });
        return this;
    }

    public SkillScript When(Func<Context, bool> predicate, Action<SkillScript> then, Action<SkillScript>? @else = null)
    {
        _steps.Add(ctx =>
        {
            var sub = new SkillScript();
            if (predicate(ctx)) then(sub); else @else?.Invoke(sub);
            return sub.BuildPlan(ctx).ToList();
        });
        return this;
    }

    public SkillScript Repeat(int times, Action<SkillScript> body)
    {
        if (times <= 0) return this;
        _steps.Add(ctx =>
        {
            var result = new List<AtomicAction[]>();
            for (int i = 0; i < times; i++)
            {
                var sub = new SkillScript();
                body(sub);
                result.AddRange(sub.BuildPlan(ctx));
            }
            return result;
        });
        return this;
    }

    public SkillScript TargetUnit(Func<Context, string?> selector, Action<SkillScript, string> use)
    {
        _steps.Add(ctx =>
        {
            var id = selector(ctx);
            if (string.IsNullOrEmpty(id)) return new List<AtomicAction[]>();
            var sub = new SkillScript();
            use(sub, id!);
            return sub.BuildPlan(ctx).ToList();
        });
        return this;
    }

    public SkillScript ForEachUnits(Func<Context, IEnumerable<string>> selector, Action<SkillScript, string> body)
    {
        _steps.Add(ctx =>
        {
            var result = new List<AtomicAction[]>();
            foreach (var id in selector(ctx))
            {
                var sub = new SkillScript();
                body(sub, id);
                result.AddRange(sub.BuildPlan(ctx));
            }
            return result;
        });
        return this;
    }

    public SkillScript ForEachParallel(Func<Context, IEnumerable<string>> selector, Action<SkillScript, string> body)
    {
        _steps.Add(ctx =>
        {
            var merged = new List<AtomicAction>();
            foreach (var id in selector(ctx))
            {
                var sub = new SkillScript();
                body(sub, id);
                var plan = sub.BuildPlan(ctx);
                foreach (var batch in plan)
                    merged.AddRange(batch);
            }
            return new List<AtomicAction[]> { merged.ToArray() };
        });
        return this;
    }

    internal IReadOnlyList<AtomicAction[]> BuildPlan(Context ctx)
    {
        var list = new List<AtomicAction[]>();
        foreach (var step in _steps)
            list.AddRange(step(ctx));
        return list;
    }

    // ---------- Usability sugar ----------
    public SkillScript Damage(string targetId, int amount)
        => Do(new Damage(targetId, amount));

    public SkillScript Damage(Func<Context, string?> selector, int amount)
        => TargetUnit(selector, (s, id) => s.Do(new Damage(id, amount)));

    public SkillScript AddUnitTag(string unitId, string tag)
        => Do(new ETBBS.AddUnitTag(unitId, tag));

    public SkillScript RemoveUnitTag(string unitId, string tag)
        => Do(new ETBBS.RemoveUnitTag(unitId, tag));

    public SkillScript AddUnitTag(Func<Context, string?> selector, string tag)
        => TargetUnit(selector, (s, id) => s.Do(new ETBBS.AddUnitTag(id, tag)));

    public SkillScript RemoveUnitTag(Func<Context, string?> selector, string tag)
        => TargetUnit(selector, (s, id) => s.Do(new ETBBS.RemoveUnitTag(id, tag)));

    public SkillScript SetUnitVar(string unitId, string key, object value)
        => Do(new ETBBS.SetUnitVar(unitId, key, value));

    public SkillScript ModifyUnitVar(string unitId, string key, Func<object, object> modifier)
        => Do(new ETBBS.ModifyUnitVar(unitId, key, modifier));

    public SkillScript SetTileVar(Coord pos, string key, object value)
        => Do(new ETBBS.SetTileVar(pos, key, value));

    public SkillScript AddTileTag(Coord pos, string tag)
        => Do(new ETBBS.AddTileTag(pos, tag));

    public SkillScript RemoveTileTag(Coord pos, string tag)
        => Do(new ETBBS.RemoveTileTag(pos, tag));

    public SkillScript SetGlobalVar(string key, object value)
        => Do(new ETBBS.SetGlobalVar(key, value));

    public SkillScript AddGlobalTag(string tag)
        => Do(new ETBBS.AddGlobalTag(tag));

    public SkillScript RemoveGlobalTag(string tag)
        => Do(new ETBBS.RemoveGlobalTag(tag));

    public SkillScript ConsumeMp(string casterId, int amount)
        => ModifyUnitVar(casterId, Keys.Mp, mp => mp switch
        {
            int i => i - amount,
            long l => (int)l - amount,
            double d => d - amount,
            null => -amount,
            _ => Convert.ToDouble(mp) - amount
        });

    public SkillScript ConsumeMp(string casterId, double amount)
        => ModifyUnitVar(casterId, Keys.Mp, mp => mp switch
        {
            double d => d - amount,
            int i => (double)i - amount,
            null => -amount,
            _ => Convert.ToDouble(mp) - amount
        });

    // Convenience: hp/mp gain/consume
    public SkillScript GainHp(string unitId, int amount) => Do(new ETBBS.Heal(unitId, amount));
    public SkillScript ConsumeHp(string unitId, int amount) => Do(new ETBBS.Damage(unitId, amount));
    public SkillScript GainMp(string unitId, double amount)
        => ModifyUnitVar(unitId, Keys.Mp, mp => mp switch
        {
            double d => d + amount,
            int i => (double)i + amount,
            null => amount,
            _ => Convert.ToDouble(mp) + amount
        });

    // Generic numeric inc/dec for unit/tile/global vars
    public SkillScript IncUnitVar(string unitId, string key, double delta)
        => ModifyUnitVar(unitId, key, v => ToNumber(v) + delta);

    public SkillScript IncTileVar(Coord pos, string key, double delta)
        => Do(ctx => new ETBBS.ModifyTileVar(pos, key, v => ToNumber(v) + delta));

    public SkillScript IncGlobalVar(string key, double delta)
        => Do(ctx => new ETBBS.ModifyGlobalVar(key, v => ToNumber(v) + delta));

    public SkillScript ClampUnitVar(string unitId, string key, double min, double max)
        => ModifyUnitVar(unitId, key, v => Clamp(ToNumber(v), min, max));

    public SkillScript ClampGlobalVar(string key, double min, double max)
        => Do(ctx => new ETBBS.ModifyGlobalVar(key, v => Clamp(ToNumber(v), min, max)));

    public SkillScript RemoveUnitVar(string unitId, string key) => Do(new ETBBS.RemoveUnitVar(unitId, key));
    public SkillScript RemoveTileVar(Coord pos, string key) => Do(new ETBBS.RemoveTileVar(pos, key));
    public SkillScript RemoveGlobalVar(string key) => Do(new ETBBS.RemoveGlobalVar(key));

    private static double ToNumber(object? v)
        => v switch { null => 0.0, int i => i, long l => l, double d => d, float f => f, _ => Convert.ToDouble(v) };
    private static double Clamp(double x, double a, double b) => Math.Max(a, Math.Min(b, x));

    // Predicated sugar
    public SkillScript If(Func<Context, bool> predicate, Action<SkillScript> then, Action<SkillScript>? @else = null)
        => When(predicate, then, @else);

    public SkillScript IfHasUnitTag(string unitId, string tag, Action<SkillScript> then, Action<SkillScript>? @else = null)
        => When(ctx => ctx.HasUnitTag(unitId, tag), then, @else);

    public SkillScript IfTargetHasTag(Func<Context, string?> selector, string tag, Action<SkillScript> then, Action<SkillScript>? @else = null)
    {
        _steps.Add(ctx =>
        {
            var id = selector(ctx);
            var sub = new SkillScript();
            if (!string.IsNullOrEmpty(id) && ctx.HasUnitTag(id!, tag)) then(sub); else @else?.Invoke(sub);
            return sub.BuildPlan(ctx).ToList();
        });
        return this;
    }

    // Iteration sugar
    public SkillScript ForEachEnemiesInRange(string casterId, IReadOnlyDictionary<string, string> teamMap, int range, Action<SkillScript, string> body, DistanceMetric metric = DistanceMetric.Manhattan, string posKey = Keys.Pos)
        => ForEachUnits(Selection.EnemiesWithinRange(casterId, teamMap, range, metric, posKey), body);

    public SkillScript ForEachEnemiesInRangeParallel(string casterId, IReadOnlyDictionary<string, string> teamMap, int range, Action<SkillScript, string> body, DistanceMetric metric = DistanceMetric.Manhattan, string posKey = Keys.Pos)
        => ForEachParallel(Selection.EnemiesWithinRange(casterId, teamMap, range, metric, posKey), body);
}



