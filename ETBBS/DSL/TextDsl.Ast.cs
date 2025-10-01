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
        public int Pos;
        public readonly List<IStmt> Items = new();
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            foreach (var i in Items) i.Emit(s, opts, getIt, setIt);
        }
    }

    private sealed class ParallelStmt : IStmt
    {
        public int Pos;
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
        public int Pos;
        public int Times;
        public IStmt Body = new BlockStmt();
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            s.Repeat(Times, sub =>
            {
                var trace = TraceExtensions.CurrentTrace;
                using (trace?.TraceScope($"repeat {Times} times"))
                {
                    Body.Emit(sub, opts, getIt, setIt);
                }
            });
        }
    }

    private sealed class IfStmt : IStmt
    {
        public int Pos;
        public required CondExpr Cond;
        public IStmt Then = new BlockStmt();
        public IStmt? Else;
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            s.If(ctx =>
                {
                    var result = Cond.Eval(ctx, opts, getIt());
                    var trace = TraceExtensions.CurrentTrace;
                    trace?.LogCondition(Cond.ToString() ?? "condition", result);
                    return result;
                },
                then: sub => Then.Emit(sub, opts, getIt, setIt),
                @else: Else is null ? null : new Action<SkillScript>(sub => Else.Emit(sub, opts, getIt, setIt)));
        }
    }

    private sealed class ChanceStmt : IStmt
    {
        public int Pos;
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
                    var prob = Math.Clamp(Probability, 0.0, 1.0);
                    var result = v < prob;
                    var trace = TraceExtensions.CurrentTrace;
                    trace?.LogCondition($"chance {prob * 100:F0}%", result, new Dictionary<string, object> { ["roll"] = v });
                    return result;
                },
                then: sub => Then.Emit(sub, opts, getIt, setIt),
                @else: Else is null ? null : new Action<SkillScript>(sub => Else.Emit(sub, opts, getIt, setIt))
            );
        }
    }

    private sealed class ForEachStmt : IStmt
    {
        public int Pos;
        public required SelectorExpr Selector;
        public bool Parallel;
        public IStmt Body = new BlockStmt();
        public void Emit(SkillScript s, TextDslOptions opts, Func<string?> getIt, Action<string?> setIt)
        {
            var sel = Selector.Build(opts);

            // Wrap selector to add tracing
            Func<Context, IEnumerable<string>> tracedSel = ctx =>
            {
                var selected = sel(ctx).ToList();
                var trace = TraceExtensions.CurrentTrace;
                trace?.LogSelector(Selector.GetDescription(), selected);
                return selected;
            };

            if (Parallel)
            {
                s.ForEachParallel(tracedSel, (sub, id) =>
                {
                    var trace = TraceExtensions.CurrentTrace;
                    using (trace?.TraceScope($"parallel iteration: {id}"))
                    {
                        string? itLocal = id;
                        Body.Emit(sub, opts, () => itLocal, v => itLocal = v);
                    }
                });
            }
            else
            {
                s.ForEachUnits(tracedSel, (sub, id) =>
                {
                    var trace = TraceExtensions.CurrentTrace;
                    using (trace?.TraceScope($"iteration: {id}"))
                    {
                        string? itLocal = id;
                        Body.Emit(sub, opts, () => itLocal, v => itLocal = v);
                    }
                });
            }
        }
    }

    private sealed class ActionStmt : IStmt
    {
        public int Pos = 0;
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
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) =>
                    {
                        sub.Do(ctx =>
                        {
                            var trace = TraceExtensions.CurrentTrace;
                            trace?.LogDamage("caster", id, IntArg, "true");
                            return new Damage(id, IntArg);
                        });
                    });
                    break;
                case ActionKind.Heal:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) =>
                    {
                        sub.Do(ctx =>
                        {
                            var trace = TraceExtensions.CurrentTrace;
                            trace?.LogHeal(id, IntArg);
                            return new Heal(id, IntArg);
                        });
                    });
                    break;
                case ActionKind.DealPhysicalDamage:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, tid) =>
                    {
                        sub.Do(ctx =>
                        {
                            var src = FromUnit?.ResolveId(ctx, opts, getIt()) ?? opts.ResolveCasterId(ctx);
                            var trace = TraceExtensions.CurrentTrace;
                            trace?.LogDamage(src, tid, IntArg, "physical");
                            return new PhysicalDamage(src, tid, IntArg, RatioArg);
                        });
                    });
                    break;
                case ActionKind.DealMagicDamage:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, tid) =>
                    {
                        sub.Do(ctx =>
                        {
                            var src = FromUnit?.ResolveId(ctx, opts, getIt()) ?? opts.ResolveCasterId(ctx);
                            var trace = TraceExtensions.CurrentTrace;
                            trace?.LogDamage(src, tid, IntArg, "magic");
                            return new MagicDamage(src, tid, IntArg, RatioArg);
                        });
                    });
                    break;
                case ActionKind.AddUnitTag:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) =>
                    {
                        var trace = TraceExtensions.CurrentTrace;
                        trace?.LogAction("add tag", $"{StrArg} to {id}");
                        sub.AddUnitTag(_ => id, StrArg);
                    });
                    break;
                case ActionKind.RemoveUnitTag:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) =>
                    {
                        var trace = TraceExtensions.CurrentTrace;
                        trace?.LogAction("remove tag", $"{StrArg} from {id}");
                        sub.RemoveUnitTag(_ => id, StrArg);
                    });
                    break;
                case ActionKind.MoveTo:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) =>
                    {
                        sub.Do(ctx =>
                        {
                            var trace = TraceExtensions.CurrentTrace;
                            trace?.LogAction("move", $"{id} to {PosArg}");
                            return new Move(id, PosArg);
                        });
                    });
                    break;
                case ActionKind.DashTowards:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, tid) =>
                    {
                        sub.Do(ctx =>
                        {
                            var caster = opts.ResolveCasterId(ctx);
                            var trace = TraceExtensions.CurrentTrace;
                            trace?.LogAction("dash", $"{caster} towards {tid} (max {IntArg})");
                            return new DashTowards(caster, tid, IntArg);
                        });
                    });
                    break;
                case ActionKind.ConsumeMp:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) =>
                    {
                        var trace = TraceExtensions.CurrentTrace;
                        trace?.LogAction("consume mp", $"{id} consumes {NumArg} MP");
                        sub.ConsumeMp(id, NumArg);
                    });
                    break;
                case ActionKind.SetUnitVar:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) =>
                    {
                        sub.Do(ctx =>
                        {
                            var oldValue = ctx.GetUnitVar<object>(id, KeyArg, 0);
                            var newValue = ResolveValue(ctx, opts, getIt, ValueArg!);
                            var trace = TraceExtensions.CurrentTrace;
                            trace?.LogVariable($"{id}.{KeyArg}", oldValue, newValue);
                            return new ETBBS.SetUnitVar(id, KeyArg, newValue);
                        });
                    });
                    break;
                case ActionKind.AddTileTag:
                    s.AddTileTag(PosArg, StrArg);
                    break;
                case ActionKind.RemoveTileTag:
                    s.RemoveTileTag(PosArg, StrArg);
                    break;
                case ActionKind.SetTileVar:
                    s.Do(ctx =>
                    {
                        var newValue = ResolveValue(ctx, opts, getIt, ValueArg!);
                        var trace = TraceExtensions.CurrentTrace;
                        trace?.LogAction("set tile var", $"tile[{PosArg}].{KeyArg} = {newValue}");
                        return new ETBBS.SetTileVar(PosArg, KeyArg, newValue);
                    });
                    break;
                case ActionKind.SetGlobalVar:
                    s.Do(ctx =>
                    {
                        var oldValue = ctx.GetGlobalVar<object>(KeyArg, 0);
                        var newValue = ResolveValue(ctx, opts, getIt, ValueArg!);
                        var trace = TraceExtensions.CurrentTrace;
                        trace?.LogVariable($"global.{KeyArg}", oldValue, newValue);
                        return new ETBBS.SetGlobalVar(KeyArg, newValue);
                    });
                    break;
                case ActionKind.AddGlobalTag:
                    s.AddGlobalTag(StrArg);
                    break;
                case ActionKind.RemoveGlobalTag:
                    s.RemoveGlobalTag(StrArg);
                    break;
                case ActionKind.RemoveUnitVar:
                    s.TargetUnit(Target.BuildSelector(opts, getIt), (sub, id) => sub.RemoveUnitVar(id, KeyArg));
                    break;
                case ActionKind.RemoveTileVar:
                    s.RemoveTileVar(PosArg, KeyArg);
                    break;
                case ActionKind.RemoveGlobalVar:
                    s.RemoveGlobalVar(KeyArg);
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
        , RemoveUnitVar
        , RemoveTileVar
        , RemoveGlobalVar
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

    private sealed class HpCompareCond : CondExpr
    {
        public required UnitRef Unit;
        public required string Op;
        public required int Value;
        public override bool Eval(Context ctx, TextDslOptions opts, string? it)
        {
            var id = Unit.ResolveId(ctx, opts, it);
            var hp = id is null ? 0 : ctx.GetUnitVar<int>(id, Keys.Hp, 0);
            return Op switch
            {
                ">=" => hp >= Value,
                ">" => hp > Value,
                "<=" => hp <= Value,
                "<" => hp < Value,
                "==" => hp == Value,
                "!=" => hp != Value,
                _ => false
            };
        }
    }

    private sealed class VarCompareCond : CondExpr
    {
        public required UnitRef Unit;
        public required string Key;
        public required string Op;
        public required int Value;
        public override bool Eval(Context ctx, TextDslOptions opts, string? it)
        {
            var id = Unit.ResolveId(ctx, opts, it);
            if (id is null) return false;
            var val = ctx.GetUnitVar<int>(id, Key, 0);
            return Op switch
            {
                ">=" => val >= Value,
                ">" => val > Value,
                "<=" => val <= Value,
                "<" => val < Value,
                "==" => val == Value,
                "!=" => val != Value,
                _ => false
            };
        }
    }

    private abstract class SelectorExpr
    {
        public abstract Func<Context, IEnumerable<string>> Build(TextDslOptions opts);
        public abstract string GetDescription();
    }

    private sealed class CombinedSelector : SelectorExpr
    {
        public enum BaseKind { Enemies, Allies, Units }
        public required BaseKind Kind;
        public UnitRef? OfUnit;           // allies/enemies 'of' which unit; default caster
        public UnitRef? RangeOrigin;      // range origin unitRef
        public bool RangeFromPoint;       // use $point for range origin
        public int? Range;                // range value
        public enum ShapeKind { None, Circle, Cross, Line, Cone }
        public ShapeKind Shape;           // geometric shape filter
        public int? Length;               // line length
        public int? Width;                // line width (half-thickness)
        public int? AngleDeg;             // cone angle (degrees)
        public string? Dir;               // up|down|left|right
        public string? TagFilter;         // filter by tag
        public string? VarKey;            // numeric var filter
        public string? VarOp;             // ">=", ">", "<=", "<", "==", "!="
        public int? VarValue;
        public string? VarOrderKey;       // order by var key
        public bool VarOrderDesc;         // order desc
        public bool OrderByDistance;      // nearest/farthest
        public bool OrderDesc;            // farthest if true
        public UnitRef? DistanceOrigin;   // order distance origin
        public bool DistanceFromPoint;    // use $point for distance origin
        public int? Limit;                // top-N limit
        public bool RandomSelect;         // random selection mode
        public bool HealthiestSelect;     // select by highest HP
        public bool WeakestSelect;        // select by lowest HP

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
                    // Shape-based filtering
                    if (Shape == ShapeKind.Circle)
                    {
                        ids = ids.Where(id =>
                        {
                            var p = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
                            if (p.Equals(default)) return false;
                            var dx = p.X - oPos.X; var dy = p.Y - oPos.Y;
                            return (dx * dx + dy * dy) <= r * r;
                        });
                    }
                    else if (Shape == ShapeKind.Cross)
                    {
                        ids = ids.Where(id =>
                        {
                            var p = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
                            if (p.Equals(default)) return false;
                            var dx = Math.Abs(p.X - oPos.X); var dy = Math.Abs(p.Y - oPos.Y);
                            return (dx + dy) <= r && (dx == 0 || dy == 0);
                        });
                    }
                    else if (Shape == ShapeKind.Line)
                    {
                        var len = Math.Max(0, Length ?? r);
                        var w = Math.Max(0, Width ?? 0);
                        var dir = GetDirVector(ctx, Dir);
                        ids = ids.Where(id => IsInLine(ctx, id, oPos, dir, len, w));
                    }
                    else if (Shape == ShapeKind.Cone)
                    {
                        var len = r;
                        var angle = Math.Max(1, Math.Min(180, AngleDeg ?? 90));
                        var dir = GetDirVector(ctx, Dir);
                        ids = ids.Where(id => IsInCone(ctx, id, oPos, dir, len, angle));
                    }
                    else
                    {
                        // Default: metric-based range (global $distance)
                        var metric = ctx.GetGlobalVar<string>(DslRuntime.DistanceKey, "manhattan");
                        ids = ids.Where(id =>
                        {
                            var p = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
                            if (p.Equals(default)) return false;
                            int d = metric switch
                            {
                                "chebyshev" => Math.Max(Math.Abs(p.X - oPos.X), Math.Abs(p.Y - oPos.Y)),
                                "euclidean" => (int)Math.Round(Math.Sqrt((p.X - oPos.X) * (p.X - oPos.X) + (p.Y - oPos.Y) * (p.Y - oPos.Y))),
                                _ => Math.Abs(p.X - oPos.X) + Math.Abs(p.Y - oPos.Y)
                            };
                            return d <= r;
                        });
                    }
                }

                if (!string.IsNullOrEmpty(VarKey) && VarValue.HasValue && !string.IsNullOrEmpty(VarOp))
                {
                    var key = VarKey!; var op = VarOp!; var val = (double)VarValue!.Value;
                    ids = ids.Where(id =>
                    {
                        var obj = ctx.GetUnitVar<object>(id, key, null);
                        var got = TypeConversion.ToDouble(obj, double.NaN);
                        if (double.IsNaN(got)) return false;
                        return op switch
                        {
                            ">=" => got >= val,
                            ">" => got > val,
                            "<=" => got <= val,
                            "<" => got < val,
                            "==" => Math.Abs(got - val) < 1e-9,
                            "!=" => Math.Abs(got - val) >= 1e-9,
                            _ => false
                        };
                    });
                }

                // HP-based ordering
                if (HealthiestSelect)
                {
                    ids = ids.OrderByDescending(id => ctx.GetUnitVar<int>(id, Keys.Hp, 0));
                }
                else if (WeakestSelect)
                {
                    ids = ids.OrderBy(id => ctx.GetUnitVar<int>(id, Keys.Hp, int.MaxValue));
                }
                // Distance-based ordering
                else if (OrderByDistance && (DistanceOrigin is not null || DistanceFromPoint))
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
                // Variable-based ordering
                else if (!string.IsNullOrEmpty(VarOrderKey))
                {
                    var key = VarOrderKey!;
                    ids = (VarOrderDesc
                        ? ids.OrderByDescending(id => TypeConversion.ToDouble(ctx.GetUnitVar<object>(id, key, null), double.NegativeInfinity))
                        : ids.OrderBy(id => TypeConversion.ToDouble(ctx.GetUnitVar<object>(id, key, null), double.PositiveInfinity)));
                }

                // Random selection (shuffle before limit)
                if (RandomSelect)
                {
                    var rng = ctx.GetGlobalVar<System.Random>(DslRuntime.RngKey, new System.Random());
                    ids = ids.OrderBy(_ => rng.Next());
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
            if (p.Equals(default)) return int.MaxValue;
            var metric = ctx.GetGlobalVar<string>(DslRuntime.DistanceKey, "manhattan");
            return metric switch
            {
                "chebyshev" => Math.Max(Math.Abs(p.X - origin.X), Math.Abs(p.Y - origin.Y)),
                "euclidean" => (int)Math.Round(Math.Sqrt((p.X - origin.X) * (p.X - origin.X) + (p.Y - origin.Y) * (p.Y - origin.Y))),
                _ => Math.Abs(p.X - origin.X) + Math.Abs(p.Y - origin.Y)
            };
        }

        private static (int x, int y) GetDirVector(Context ctx, string? hint)
        {
            var d = hint;
            if (string.IsNullOrEmpty(d)) d = ctx.GetGlobalVar<string>(DslRuntime.DirKey, "");
            d = (d ?? "").ToLowerInvariant();
            return d switch
            {
                "up" => (0, -1),
                "down" => (0, 1),
                "left" => (-1, 0),
                "right" => (1, 0),
                _ => (0, -1)
            };
        }
        private static bool IsInLine(Context ctx, string id, Coord origin, (int x, int y) dir, int len, int width)
        {
            var p = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
            if (p.Equals(default)) return false;
            var dx = p.X - origin.X; var dy = p.Y - origin.Y;
            if (dir.x == 0 && dir.y != 0)
            {
                // vertical
                if (Math.Sign(dy) != Math.Sign(dir.y)) return false;
                if (Math.Abs(dx) > width) return false;
                return Math.Abs(dy) <= len;
            }
            else if (dir.y == 0 && dir.x != 0)
            {
                // horizontal
                if (Math.Sign(dx) != Math.Sign(dir.x)) return false;
                if (Math.Abs(dy) > width) return false;
                return Math.Abs(dx) <= len;
            }
            return false;
        }
        private static bool IsInCone(Context ctx, string id, Coord origin, (int x, int y) dir, int len, int angleDeg)
        {
            var p = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
            if (p.Equals(default)) return false;
            var vx = p.X - origin.X; var vy = p.Y - origin.Y;
            var dist2 = vx * vx + vy * vy;
            if (dist2 > len * len) return false;
            // Map dir to unit vector
            double ux = dir.x; double uy = dir.y;
            double dot = vx * ux + vy * uy;
            double vlen = Math.Sqrt(dist2);
            double ulen = Math.Sqrt(ux * ux + uy * uy);
            if (vlen == 0 || ulen == 0) return false;
            double cosang = dot / (vlen * ulen);
            double ang = Math.Acos(Math.Clamp(cosang, -1.0, 1.0)) * 180.0 / Math.PI;
            return ang <= angleDeg / 2.0 + 1e-9;
        }

        public override string GetDescription()
        {
            var parts = new List<string>();

            // Prefix
            if (RandomSelect) parts.Add("random");
            else if (HealthiestSelect) parts.Add("healthiest");
            else if (WeakestSelect) parts.Add("weakest");
            else if (OrderByDistance)
            {
                parts.Add(OrderDesc ? "farthest" : "nearest");
                if (Limit.HasValue) parts.Add(Limit.Value.ToString());
            }

            // Base kind
            parts.Add(Kind.ToString().ToLowerInvariant());

            // Clauses
            if (Range.HasValue) parts.Add($"in range {Range.Value}");
            if (!string.IsNullOrEmpty(TagFilter)) parts.Add($"with tag \"{TagFilter}\"");
            if (!string.IsNullOrEmpty(VarKey)) parts.Add($"with var \"{VarKey}\" {VarOp} {VarValue}");
            if (!string.IsNullOrEmpty(VarOrderKey)) parts.Add($"order by var \"{VarOrderKey}\" {(VarOrderDesc ? "desc" : "asc")}");
            if (Limit.HasValue && !OrderByDistance && !RandomSelect && !HealthiestSelect && !WeakestSelect)
                parts.Add($"limit {Limit.Value}");

            return string.Join(" ", parts);
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
        public string? TargetMode; // "tile" | "point" when targeting non-unit
        public string? Distance;   // "manhattan" | "chebyshev" | "euclidean"
        public int? MinRange;
        public int? SealedUntil;
        public int? SealedUntilDay;
        public int? SealedUntilPhase;
        public bool EndsTurn;
    }

    // simple arithmetic expression node for values
    private sealed class ArithExpr
    {
        public required object Left;
        public required object Right;
        public required char Op; // '+', '-', '*', '/'
    }

    private sealed class VarRef
    {
        public required string Key;
        public required UnitRef Unit;
    }

    private sealed class FunctionCall
    {
        public required string Name; // min, max, abs, floor, ceil, round
        public required List<object> Args;
    }
}
