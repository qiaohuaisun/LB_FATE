namespace ETBBS;

public static partial class TextDsl
{
    public static Skill FromText(string name, string script, TextDslOptions options)
    {
        var parser = new Parser(script);
        var prog = parser.ParseProgram();
        Action<SkillScript> build = ss =>
        {
            string? itVar = null; // iteration variable for bodies
            foreach (var st in prog.Statements)
                st.Emit(ss, options, () => itVar, v => itVar = v);
        };
        var builder = SkillBuilder.Create(name);
        if (prog.MpCost.HasValue) builder = builder.Cost(prog.MpCost.Value);
        if (prog.Range.HasValue) builder = builder.Range(prog.Range.Value);
        if (prog.Cooldown.HasValue) builder = builder.WithExtra("cooldown", prog.Cooldown.Value);
        if (!string.IsNullOrEmpty(prog.Targeting)) builder = builder.WithExtra("targeting", prog.Targeting!);
        if (prog.MinRange.HasValue) builder = builder.WithExtra("min_range", prog.MinRange.Value);
        if (prog.SealedUntilDay.HasValue)
        {
            builder = builder.WithExtra("sealed_until_day", prog.SealedUntilDay.Value);
            if (prog.SealedUntilPhase.HasValue) builder = builder.WithExtra("sealed_until_phase", prog.SealedUntilPhase.Value);
        }
        else if (prog.SealedUntil.HasValue)
        {
            builder = builder.WithExtra("sealed_until", prog.SealedUntil.Value);
        }
        if (prog.EndsTurn) builder = builder.WithExtra("ends_turn", true);
        return builder.Script(build).Build();
    }

    // Helper: build skill using runtime globals for caster/target/teams
    public static Skill FromTextUsingGlobals(string name, string script)
    {
        var opts = new TextDslOptions
        {
            ResolveCasterId = ctx => ctx.GetGlobalVar<string>(DslRuntime.CasterKey, string.Empty),
            ResolveTargetUnitId = ctx => ctx.GetGlobalVar<string?>(DslRuntime.TargetKey, null),
            ResolveTeamMap = ctx => ctx.GetGlobalVar<IReadOnlyDictionary<string, string>>(DslRuntime.TeamsKey, new Dictionary<string, string>())
        };
        return FromText(name, script, opts);
    }

    // ---------- Value resolution helpers (emit-time) ----------
    private static object ResolveValue(Context ctx, TextDslOptions opts, Func<string?> getIt, object value)
    {
        if (value is VarRef vr)
        {
            var id = vr.Unit.ResolveId(ctx, opts, getIt());
            if (id is null) return 0;
            return ctx.GetUnitVar<object>(id, vr.Key, 0);
        }
        if (value is ArithExpr ae)
        {
            var l = ResolveValue(ctx, opts, getIt, ae.Left);
            var r = ResolveValue(ctx, opts, getIt, ae.Right);
            double ld = ToNumber(l);
            double rd = ToNumber(r);
            double res = ae.Op == '-' ? ld - rd : ld + rd;
            if (IsIntegral(l) && IsIntegral(r) && Math.Abs(res - Math.Round(res)) < 1e-9)
                return (int)Math.Round(res);
            return res;
        }
        return value;
    }

    private static bool IsIntegral(object o)
        => o is sbyte or byte or short or ushort or int or uint or long or ulong;

    private static double ToNumber(object o)
    {
        if (o is sbyte sb) return sb;
        if (o is byte b) return b;
        if (o is short s16) return s16;
        if (o is ushort us16) return us16;
        if (o is int i32) return i32;
        if (o is uint ui32) return ui32;
        if (o is long i64) return i64;
        if (o is ulong ui64) return ui64;
        if (o is float f) return f;
        if (o is double d0) return d0;
        if (o is decimal dec) return (double)dec;
        if (o is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d1)) return d1;
        return 0.0;
    }
}
