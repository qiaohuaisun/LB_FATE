namespace ETBBS;

public sealed class TextDslOptions
{
    public required Func<Context, string> ResolveCasterId { get; init; }
    public Func<Context, string?> ResolveTargetUnitId { get; init; } = _ => null;
    public Func<Context, IReadOnlyDictionary<string, string>> ResolveTeamMap { get; init; } = _ => new Dictionary<string, string>();
}

public static partial class TextDsl
{
    // ----------------- AST -----------------
    private interface IStmt
    {
        void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt);
    }

    private sealed class BlockStmt : IStmt
    {
        public readonly List<IStmt> Items = new();
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            foreach (var i in Items) i.Emit(s, opts, getIt, setIt);
        }
    }

    private sealed class ParallelStmt : IStmt
    {
        public List<IStmt> Branches { get; } = new();
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            s.Parallel(
                Branches.Select<IStmt, Action<SkillScript>>(br =>
                    (sub) => { br.Emit(sub, opts, getIt, setIt); })
                .ToArray());
        }
    }

    private sealed class RepeatStmt : IStmt
    {
        public int Times;
        public IStmt Body = new BlockStmt();
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            s.Repeat(Times, sub => Body.Emit(sub, opts, getIt, setIt));
        }
    }

    private sealed class IfStmt : IStmt
    {
        public required CondExpr Cond;
        public IStmt Then = new BlockStmt();
        public IStmt? Else;
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            s.If(ctx => Cond.Eval(ctx, opts, getIt()),
                then: sub => Then.Emit(sub, opts, getIt, setIt),
                @else: Else is null ? null : new Action<SkillScript>(sub => Else.Emit(sub, opts, getIt, setIt)));
        }
    }

    private sealed class ChanceStmt : IStmt
    {
        public double Probability; // 0..1
        public IStmt Then = new BlockStmt();
        public IStmt? Else;
        private static readonly System.Random FallbackRng = new System.Random();
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            s.When(
                predicate: ctx =>
                {
                    var rng = ctx.GetGlobalVar<System.Random>(DslRuntime.RngKey, null!);
                    double v = (rng is not null) ? rng.NextDouble() : FallbackRng.NextDouble();
                    return v < Math.Clamp(Probability, 0.0, 1.0);
                },
                then: sub => Then.Emit(sub, opts, getIt, setIt),
                @else: Else is null ? null : new Action<SkillScript>(sub => Else.Emit(sub, opts, getIt, setIt))
            );
        }
    }

    private sealed class ForEachStmt : IStmt
    {
        public required SelectorExpr Selector;
        public bool Parallel;
        public IStmt Body = new BlockStmt();
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            var sel = Selector.Build(opts);
            if (Parallel)
            {
                s.ForEachParallel(sel, (sub, id) =>
                {
                    string? itLocal = id;
                    Body.Emit(sub, opts, () => itLocal, v => itLocal = v);
                });
            }
            else
            {
                s.ForEachUnits(sel, (sub, id) =>
                {
                    string? itLocal = id;
                    Body.Emit(sub, opts, () => itLocal, v => itLocal = v);
                });
            }
        }
    }

    private sealed class ActionStmt : IStmt
    {
        public required ActionKind Kind;
        public int IntArg;
        public int IntArg2;
        public int IntArg3;
        public double NumArg;
        public string StrArg = "";
        public string KeyArg = "";
        public Coord PosArg;
        public required UnitRef Target;
        public object? ValueArg;
        public UnitRef? FromUnit;
        public double RatioArg; // e.g., ignore defense percent as 0..1

        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            switch (Kind)
            {
                case ActionKind.DealDamage:
                    s.Damage(Target.BuildSelector(opts, getIt), IntArg);
                    break;
                case ActionKind.Heal:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) => sub.Do(new Heal(id, IntArg)));
                    break;
                case ActionKind.DealPhysicalDamage:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, tid) =>
                    {
                        sub.Do(ctx => new PhysicalDamage(
                            FromUnit?.ResolveId(ctx, opts, getIt()) ?? opts.ResolveCasterId(ctx),
                            tid,
                            IntArg,
                            RatioArg));
                    });
                    break;
                case ActionKind.DealMagicDamage:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, tid) =>
                    {
                        sub.Do(ctx => new MagicDamage(
                            FromUnit?.ResolveId(ctx, opts, getIt()) ?? opts.ResolveCasterId(ctx),
                            tid,
                            IntArg,
                            RatioArg));
                    });
                    break;
                case ActionKind.AddUnitTag:
                    s.AddUnitTag(Target.BuildSelector(opts, getIt), StrArg);
                    break;
                case ActionKind.RemoveUnitTag:
                    s.RemoveUnitTag(Target.BuildSelector(opts, getIt), StrArg);
                    break;
                case ActionKind.MoveTo:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) => sub.Do(new Move(id, PosArg)));
                    break;
                case ActionKind.DashTowards:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, tid) =>
                    {
                        sub.Do(ctx => new DashTowards(opts.ResolveCasterId(ctx), tid, IntArg));
                    });
                    break;
                case ActionKind.ConsumeMp:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) => sub.ConsumeMp(id, NumArg));
                    break;
                case ActionKind.SetUnitVar:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) => sub.Do(ctx => new ETBBS.SetUnitVar(id, KeyArg, ResolveValue(ctx, opts, getIt, ValueArg!))));
                    break;
                case ActionKind.AddTileTag:
                    s.AddTileTag(PosArg, StrArg);
                    break;
                case ActionKind.RemoveTileTag:
                    s.RemoveTileTag(PosArg, StrArg);
                    break;
                case ActionKind.SetTileVar:
                    s.Do(ctx => new ETBBS.SetTileVar(PosArg, KeyArg, ResolveValue(ctx, opts, getIt, ValueArg!)));
                    break;
                case ActionKind.SetGlobalVar:
                    s.Do(ctx => new ETBBS.SetGlobalVar(KeyArg, ResolveValue(ctx, opts, getIt, ValueArg!)));
                    break;
                case ActionKind.AddGlobalTag:
                    s.AddGlobalTag(StrArg);
                    break;
                case ActionKind.RemoveGlobalTag:
                    s.RemoveGlobalTag(StrArg);
                    break;
                case ActionKind.LinePhysicalAoe:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, tid) =>
                    {
                        sub.Do(ctx => new LineAoeDamage(opts.ResolveCasterId(ctx), tid, IntArg, IntArg2, IntArg3, DamageFlavor.Physical, IgnoreRatio: RatioArg));
                    });
                    break;
                case ActionKind.LineMagicAoe:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, tid) =>
                    {
                        sub.Do(ctx => new LineAoeDamage(opts.ResolveCasterId(ctx), tid, IntArg, IntArg2, IntArg3, DamageFlavor.Magic, IgnoreRatio: RatioArg));
                    });
                    break;
                case ActionKind.LineTrueAoe:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, tid) =>
                    {
                        sub.Do(ctx => new LineAoeDamage(opts.ResolveCasterId(ctx), tid, IntArg, IntArg2, IntArg3, DamageFlavor.True));
                    });
                    break;
            }
        }
    }

    private enum ActionKind
    {
        DealDamage,
        Heal,
        DealPhysicalDamage,
        DealMagicDamage,
        AddUnitTag,
        RemoveUnitTag,
        MoveTo,
        DashTowards,
        LinePhysicalAoe,
        LineMagicAoe,
        LineTrueAoe,
        ConsumeMp,
        SetUnitVar,
        AddTileTag,
        RemoveTileTag,
        SetTileVar,
        SetGlobalVar,
        AddGlobalTag,
        RemoveGlobalTag
    }

    private abstract class CondExpr
    {
        public abstract bool Eval(Context ctx, TextDslOptions opts, string? it);
    }

    private sealed class HasTagCond : CondExpr
    {
        public required UnitRef Unit;
        public required string Tag;
        public override bool Eval(Context ctx, TextDslOptions opts, string? it)
        {
            var id = Unit.ResolveId(ctx, opts, it);
            return id is not null && ctx.HasUnitTag(id, Tag);
        }
    }

    private sealed class MpCompareCond : CondExpr
    {
        public required UnitRef Unit;
        public required string Op; // ">=", ">", "<=", "<", "==", "!="
        public required int Value;
        public override bool Eval(Context ctx, TextDslOptions opts, string? it)
        {
            var id = Unit.ResolveId(ctx, opts, it);
            var mp = id is null ? 0 : ctx.GetUnitVar<int>(id, Keys.Mp, 0);
            return Op switch
            {
                ">=" => mp >= Value,
                ">" => mp > Value,
                "<=" => mp <= Value,
                "<" => mp < Value,
                "==" => mp == Value,
                "!=" => mp != Value,
                _ => false
            };
        }
    }

    private abstract class SelectorExpr
    {
        public abstract Func<Context, IEnumerable<string>> Build(TextDslOptions opts);
    }

    private sealed class CombinedSelector : SelectorExpr
    {
        public enum BaseKind { Enemies, Allies, Units }
        public required BaseKind Kind;
        public UnitRef? OfUnit;           // allies/enemies 'of' which unit; default caster
        public UnitRef? RangeOrigin;      // range origin unitRef
        public bool RangeFromPoint;       // use $point for range origin
        public int? Range;                // range value
        public string? TagFilter;         // filter by tag
        public string? VarKey;            // numeric var filter
        public string? VarOp;             // ">=", ">", "<=", "<", "==", "!="
        public int? VarValue;
        public bool OrderByDistance;      // nearest/farthest
        public bool OrderDesc;            // farthest if true
        public UnitRef? DistanceOrigin;   // order distance origin
        public bool DistanceFromPoint;    // use $point for distance origin
        public int? Limit;                // top-N limit

        public override Func<Context, IEnumerable<string>> Build(TextDslOptions opts)
            => ctx =>
            {
                IEnumerable<string> ids = ctx.State.Units.Keys;
                var teamMap = opts.ResolveTeamMap(ctx);

                string? ofId = OfUnit?.ResolveId(ctx, opts, null) ?? opts.ResolveCasterId(ctx);
                switch (Kind)
                {
                    case BaseKind.Enemies:
                        if (ofId is null || !teamMap.TryGetValue(ofId, out var teamE)) return Array.Empty<string>();
                        ids = ids.Where(id => id != ofId && teamMap.TryGetValue(id, out var t) && t != teamE);
                        break;
                    case BaseKind.Allies:
                        if (ofId is null || !teamMap.TryGetValue(ofId, out var teamA)) return Array.Empty<string>();
                        ids = ids.Where(id => id != ofId && teamMap.TryGetValue(id, out var t) && t == teamA);
                        break;
                    case BaseKind.Units:
                        break;
                }

                if (!string.IsNullOrEmpty(TagFilter))
                {
                    var tag = TagFilter!;
                    ids = ids.Where(id => ctx.State.Units.TryGetValue(id, out var u) && u.Tags.Contains(tag));
                }

                if (Range.HasValue && (RangeOrigin is not null || RangeFromPoint))
                {
                    Coord oPos;
                    if (RangeFromPoint)
                    {
                        oPos = ctx.GetGlobalVar<Coord>(DslRuntime.TargetPointKey, default);
                    }
                    else
                    {
                        var originId = RangeOrigin!.ResolveId(ctx, opts, null);
                        if (originId is null) return Array.Empty<string>();
                        oPos = ctx.GetUnitVar<Coord>(originId, Keys.Pos, default);
                    }
                    if (oPos.Equals(default(Coord))) return Array.Empty<string>();
                    int r = Range.Value;
                    ids = ids.Where(id =>
                    {
                        var p = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
                        return !p.Equals(default(Coord)) && Math.Abs(p.X - oPos.X) + Math.Abs(p.Y - oPos.Y) <= r;
                    });
                }

                if (!string.IsNullOrEmpty(VarKey) && VarValue.HasValue && !string.IsNullOrEmpty(VarOp))
                {
                    var key = VarKey!; var op = VarOp!; var val = VarValue!.Value;
                    ids = ids.Where(id =>
                    {
                        var got = ctx.GetUnitVar<int>(id, key, int.MinValue);
                        return op switch
                        {
                            ">=" => got >= val,
                            ">" => got > val,
                            "<=" => got <= val,
                            "<" => got < val,
                            "==" => got == val,
                            "!=" => got != val,
                            _ => false
                        };
                    });
                }

                if (OrderByDistance && (DistanceOrigin is not null || DistanceFromPoint))
                {
                    Coord oPos;
                    if (DistanceFromPoint)
                    {
                        oPos = ctx.GetGlobalVar<Coord>(DslRuntime.TargetPointKey, default);
                    }
                    else
                    {
                        var originId = DistanceOrigin!.ResolveId(ctx, opts, null);
                        if (originId is null) return Array.Empty<string>();
                        oPos = ctx.GetUnitVar<Coord>(originId, Keys.Pos, default);
                    }
                    if (oPos.Equals(default(Coord))) return Array.Empty<string>();
                    ids = (OrderDesc ? ids.OrderByDescending(id => Dist(ctx, id, oPos)) : ids.OrderBy(id => Dist(ctx, id, oPos)));
                }

                if (Limit.HasValue && Limit.Value >= 0)
                {
                    ids = ids.Take(Limit.Value);
                }

                return ids;
            };

        private static int Dist(Context ctx, string id, Coord origin)
        {
            var p = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
            return p.Equals(default(Coord)) ? int.MaxValue : Math.Abs(p.X - origin.X) + Math.Abs(p.Y - origin.Y);
        }
    }

    private abstract class UnitRef
    {
        public abstract Func<Context, string?> BuildSelector(TextDslOptions opts, Func<string?> getIt);
        public abstract string? ResolveId(Context ctx, TextDslOptions opts, string? it);
    }

    private sealed class UnitRefCaster : UnitRef
    {
        public override Func<Context, string?> BuildSelector(TextDslOptions opts, Func<string?> getIt)
            => ctx => opts.ResolveCasterId(ctx);
        public override string? ResolveId(Context ctx, TextDslOptions opts, string? it) => opts.ResolveCasterId(ctx);
    }

    private sealed class UnitRefTarget : UnitRef
    {
        public override Func<Context, string?> BuildSelector(TextDslOptions opts, Func<string?> getIt)
            => ctx => opts.ResolveTargetUnitId(ctx);
        public override string? ResolveId(Context ctx, TextDslOptions opts, string? it) => opts.ResolveTargetUnitId(ctx);
    }

    private sealed class UnitRefIt : UnitRef
    {
        public override Func<Context, string?> BuildSelector(TextDslOptions opts, Func<string?> getIt)
            => ctx => getIt();
        public override string? ResolveId(Context ctx, TextDslOptions opts, string? it) => it;
    }

    private sealed class UnitRefById : UnitRef
    {
        public required string Id;
        public override Func<Context, string?> BuildSelector(TextDslOptions opts, Func<string?> getIt)
            => ctx => Id;
        public override string? ResolveId(Context ctx, TextDslOptions opts, string? it) => Id;
    }

    private sealed class ProgramNode
    {
        public int? MpCost;
        public int? Range;
        public readonly List<IStmt> Statements = new();
        public int? Cooldown;
        public string? Targeting;
        public int? MinRange;
        public int? SealedUntil;
    }

    // simple arithmetic expression node for values
    private sealed class ArithExpr
    {
        public required object Left;
        public required object Right;
        public required char Op; // '+' or '-'
    }

    private sealed class VarRef
    {
        public required string Key;
        public required UnitRef Unit;
    }
}
