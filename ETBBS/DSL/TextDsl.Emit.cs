namespace ETBBS;

public static partial class TextDsl
{
    // Lightweight static analysis for scripts. Returns human-readable warnings.
    public static List<string> AnalyzeText(string script)
    {
        var warnings = new List<string>();
        ProgramNode prog;
        try
        {
            var parser = new Parser(script);
            prog = parser.ParseProgram();
        }
        catch
        {
            // If it doesn't parse, validator will report syntax; skip analysis.
            return warnings;
        }

        // Helper: compute 1-based line/col from source index
        static (int line, int col) GetLineCol(string src, int pos)
        {
            int line = 1, col = 1;
            for (int i = 0; i < src.Length && i < pos; i++)
            {
                var c = src[i];
                if (c == '\r') continue;
                if (c == '\n') { line++; col = 1; }
                else col++;
            }
            if (col < 1) col = 1; return (line, col);
        }

        // Program-level checks
        if (prog.MinRange is int mn && prog.Range is int rn && mn > rn)
        {
            warnings.Add($"min_range ({mn}) exceeds range ({rn}); targets won't be valid");
        }
        if ((prog.Targeting?.Equals("self", StringComparison.OrdinalIgnoreCase) ?? false) && (prog.Range ?? 0) > 0)
        {
            warnings.Add("targeting self with non-zero range; range is ignored");
        }

        void Visit(IStmt st)
        {
            switch (st)
            {
                case ChanceStmt c:
                    if (c.Probability <= 0.0)
                    {
                        var (ln, co) = GetLineCol(script, c.Pos);
                        warnings.Add($"line {ln}, col {co}: chance 0%: then-branch is unreachable");
                    }
                    else if (c.Probability >= 1.0)
                    {
                        var (ln, co) = GetLineCol(script, c.Pos);
                        warnings.Add($"line {ln}, col {co}: chance 100%: else-branch is unreachable");
                    }
                    Visit(c.Then);
                    if (c.Else is not null) Visit(c.Else);
                    break;
                case RepeatStmt r:
                    if (r.Times <= 0)
                    {
                        var (ln, co) = GetLineCol(script, r.Pos);
                        warnings.Add($"line {ln}, col {co}: repeat {r.Times} times has no effect");
                    }
                    Visit(r.Body);
                    break;
                case ParallelStmt p:
                    if (p.Branches.Count == 0)
                    {
                        var (ln, co) = GetLineCol(script, p.Pos);
                        warnings.Add($"line {ln}, col {co}: parallel block is empty");
                    }
                    foreach (var b in p.Branches) Visit(b);
                    break;
                case BlockStmt b:
                    if (b.Items.Count == 0)
                    {
                        var (ln, co) = GetLineCol(script, b.Pos);
                        warnings.Add($"line {ln}, col {co}: empty block has no effect");
                    }
                    foreach (var i in b.Items) Visit(i);
                    break;
                case ForEachStmt f:
                    if (f.Selector is CombinedSelector cs)
                    {
                        if (cs.Range is int rr && rr < 0)
                        {
                            var (ln, co) = GetLineCol(script, f.Pos);
                            warnings.Add($"line {ln}, col {co}: selector range is negative");
                        }
                        if (cs.Limit is int lim && lim < 0)
                        {
                            var (ln, co) = GetLineCol(script, f.Pos);
                            warnings.Add($"line {ln}, col {co}: selector limit is negative");
                        }
                        // Shape checks
                        if (cs.Shape == CombinedSelector.ShapeKind.Line)
                        {
                            var (ln, co) = GetLineCol(script, f.Pos);
                            if ((cs.Length ?? 0) <= 0) warnings.Add($"line {ln}, col {co}: line length must be > 0");
                            if ((cs.Width ?? 0) < 0) warnings.Add($"line {ln}, col {co}: line width is negative");
                            if (string.IsNullOrEmpty(cs.Dir)) warnings.Add($"line {ln}, col {co}: line dir not specified (using $dir)");
                        }
                        if (cs.Shape == CombinedSelector.ShapeKind.Cone)
                        {
                            var (ln, co) = GetLineCol(script, f.Pos);
                            if (!(cs.Range is int r2) || r2 <= 0) warnings.Add($"line {ln}, col {co}: cone radius must be > 0");
                            if (!(cs.AngleDeg is int a) || a <= 0) warnings.Add($"line {ln}, col {co}: cone angle not specified (default ~90)");
                            if (string.IsNullOrEmpty(cs.Dir)) warnings.Add($"line {ln}, col {co}: cone dir not specified (using $dir)");
                        }
                    }
                    Visit(f.Body);
                    break;
                case ActionStmt:
                    break;
            }
        }

        foreach (var st in prog.Statements) Visit(st);
        return warnings;
    }
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
        if (!string.IsNullOrEmpty(prog.TargetMode)) builder = builder.WithExtra("target_mode", prog.TargetMode!);
        if (!string.IsNullOrEmpty(prog.Distance)) builder = builder.WithExtra("distance", prog.Distance!);
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
            double res = ae.Op switch
            {
                '+' => ld + rd,
                '-' => ld - rd,
                '*' => ld * rd,
                '/' => Math.Abs(rd) > 1e-9 ? ld / rd : 0.0,
                '%' => Math.Abs(rd) > 1e-9 ? ld % rd : 0.0,
                _ => 0.0
            };
            if (IsIntegral(l) && IsIntegral(r) && (ae.Op == '+' || ae.Op == '-' || ae.Op == '*') && Math.Abs(res - Math.Round(res)) < 1e-9)
                return (int)Math.Round(res);
            return res;
        }
        if (value is FunctionCall fc)
        {
            var resolvedArgs = fc.Args.Select(a => ResolveValue(ctx, opts, getIt, a)).ToList();
            return fc.Name switch
            {
                "min" => resolvedArgs.Count > 0 ? resolvedArgs.Min(ToNumber) : 0.0,
                "max" => resolvedArgs.Count > 0 ? resolvedArgs.Max(ToNumber) : 0.0,
                "abs" => resolvedArgs.Count > 0 ? Math.Abs(ToNumber(resolvedArgs[0])) : 0.0,
                "floor" => resolvedArgs.Count > 0 ? Math.Floor(ToNumber(resolvedArgs[0])) : 0.0,
                "ceil" => resolvedArgs.Count > 0 ? Math.Ceiling(ToNumber(resolvedArgs[0])) : 0.0,
                "round" => resolvedArgs.Count > 0 ? Math.Round(ToNumber(resolvedArgs[0])) : 0.0,
                _ => 0.0
            };
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
