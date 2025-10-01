using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ETBBS;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run(new[] { typeof(LbrParseBenchmarks), typeof(DslBenchmarks), typeof(RuntimeBenchmarks) });
    }
}

[MemoryDiagnoser]
public class LbrParseBenchmarks
{
    private string _lbr = "role \"T\" id \"t\" { vars { \"hp\" = 30 } skills { skill \"S\" { deal 5 damage to target } } }";

    [GlobalSetup]
    public void Setup()
    {
        var file = System.IO.Path.Combine("publish", "roles", "kate.lbr");
        if (System.IO.File.Exists(file)) _lbr = System.IO.File.ReadAllText(file);
    }

    [Benchmark]
    public RoleDefinition ParseRole() => LbrLoader.Load(_lbr);
}

[MemoryDiagnoser]
public class DslBenchmarks
{
    private string _script = "range 2; targeting enemies; deal 5 damage to target;";

    [Benchmark]
    public Skill CompileSkill() => TextDsl.FromTextUsingGlobals("S", _script);
}

[MemoryDiagnoser]
public class RuntimeBenchmarks
{
    private WorldState _s = WorldState.CreateEmpty(10, 10);

    [GlobalSetup]
    public void Setup()
    {
        _s = WorldStateOps.WithUnit(_s, "A", _ => new UnitState(
            Vars: System.Collections.Immutable.ImmutableDictionary<string, object>.Empty,
            Tags: System.Collections.Immutable.ImmutableHashSet<string>.Empty));
        _s = WorldStateOps.WithUnit(_s, "B", _ => new UnitState(
            Vars: System.Collections.Immutable.ImmutableDictionary<string, object>.Empty,
            Tags: System.Collections.Immutable.ImmutableHashSet<string>.Empty));
        _s = WorldStateOps.WithUnit(_s, "A", u => u with { Vars = u.Vars.SetItem(Keys.Hp, 100).SetItem(Keys.Atk, 10) });
        _s = WorldStateOps.WithUnit(_s, "B", u => u with { Vars = u.Vars.SetItem(Keys.Hp, 100).SetItem(Keys.Def, 2) });
    }

    [Benchmark]
    public WorldState ApplyPhysical() => new PhysicalDamage("A", "B", 7).Compile()(_s);
}
