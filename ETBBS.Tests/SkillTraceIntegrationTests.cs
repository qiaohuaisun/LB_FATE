using ETBBS;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

public class SkillTraceIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public SkillTraceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static WorldState EmptyWorld(int w = 10, int h = 10)
        => WorldState.CreateEmpty(w, h);

    private static WorldState WithUnit(WorldState s, string id, IDictionary<string, object> vars)
    {
        var imVars = ImmutableDictionary.CreateRange(vars);
        return WorldStateOps.WithUnit(s, id, _ => new UnitState(imVars, ImmutableHashSet<string>.Empty));
    }

    [Fact]
    public void Trace_Captures_Selector_And_Damage()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1), [Keys.Hp] = 100 });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1), [Keys.Hp] = 50 });
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1), [Keys.Hp] = 30 });
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
        });

        // Create trace
        var trace = new SkillTrace(enabled: true);
        TraceExtensions.CurrentTrace = trace;

        var script = "for each enemies within 5 do { deal physical 10 damage to it from caster }";
        var skill = TextDsl.FromTextUsingGlobals("TestSkill", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // Verify trace captured events
        Assert.NotEmpty(trace.Entries);

        // Should have selector log
        var selectorEntries = trace.Entries.Where(e => e.Type == "Selector").ToList();
        Assert.Single(selectorEntries);
        Assert.Contains("enemies", selectorEntries[0].Message);
        Assert.Equal(2, selectorEntries[0].Data["count"]); // 2 enemies selected

        // Should have damage logs
        var damageEntries = trace.Entries.Where(e => e.Type == "Damage").ToList();
        Assert.Equal(2, damageEntries.Count); // 2 damages dealt

        // Output trace for debugging
        _output.WriteLine(trace.FormatTrace(verbose: true));

        // Clear for next test
        TraceExtensions.CurrentTrace = null;
    }

    [Fact]
    public void Trace_Captures_Condition_And_Variables()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object>
        {
            [Keys.Pos] = new Coord(1, 1),
            [Keys.Hp] = 30,
            [Keys.Mp] = 5,
            ["atk"] = 10
        });
        var teams = new Dictionary<string, string> { ["C"] = "T1" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
        });

        var trace = new SkillTrace(enabled: true);
        TraceExtensions.CurrentTrace = trace;

        var script = @"
            if caster hp < 50 then {
                set unit(caster) var ""atk"" = var ""atk"" of caster * 2
            }
        ";
        var skill = TextDsl.FromTextUsingGlobals("PowerUp", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // Verify trace captured condition
        var condEntries = trace.Entries.Where(e => e.Type == "Condition").ToList();
        Assert.Single(condEntries);
        Assert.True((bool)condEntries[0].Data["result"]); // Condition was true

        // Verify trace captured variable change
        var varEntries = trace.Entries.Where(e => e.Type == "Variable").ToList();
        Assert.Single(varEntries);
        Assert.Contains("atk", varEntries[0].Message);
        Assert.Equal(10, Convert.ToInt32(varEntries[0].Data["from"]));
        Assert.Equal(20, Convert.ToInt32(varEntries[0].Data["to"]));

        _output.WriteLine(trace.FormatTrace(verbose: true));

        TraceExtensions.CurrentTrace = null;
    }

    [Fact]
    public void Trace_Captures_Random_Selector()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1), [Keys.Hp] = 50 });
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1), [Keys.Hp] = 60 });
        s = WithUnit(s, "E3", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 1), [Keys.Hp] = 70 });
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["E3"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
            .SetItem(DslRuntime.RngKey, new Random(42))
        });

        var trace = new SkillTrace(enabled: true);
        TraceExtensions.CurrentTrace = trace;

        var script = "for each random 2 enemies do { deal 15 damage to it }";
        var skill = TextDsl.FromTextUsingGlobals("RandomAttack", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // Verify trace shows random selector
        var selectorEntries = trace.Entries.Where(e => e.Type == "Selector").ToList();
        Assert.Single(selectorEntries);
        Assert.Contains("random", selectorEntries[0].Message);
        Assert.Equal(2, selectorEntries[0].Data["count"]); // Exactly 2 selected

        _output.WriteLine(trace.FormatTrace(verbose: true));

        TraceExtensions.CurrentTrace = null;
    }

    [Fact]
    public void Trace_Captures_Heal_And_Weakest_Selector()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1), [Keys.Hp] = 100 });
        s = WithUnit(s, "A1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1), [Keys.Hp] = 80 });
        s = WithUnit(s, "A2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1), [Keys.Hp] = 30 }); // weakest
        var teams = new Dictionary<string, string> { ["C"] = "T", ["A1"] = "T", ["A2"] = "T" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
        });

        var trace = new SkillTrace(enabled: true);
        TraceExtensions.CurrentTrace = trace;

        var script = "for each weakest allies do { heal 20 to it }";
        var skill = TextDsl.FromTextUsingGlobals("SmartHeal", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // Verify trace shows weakest selector
        var selectorEntries = trace.Entries.Where(e => e.Type == "Selector").ToList();
        Assert.Single(selectorEntries);
        Assert.Contains("weakest", selectorEntries[0].Message);

        // Verify heal was traced
        var healEntries = trace.Entries.Where(e => e.Type == "Heal").ToList();
        Assert.Single(healEntries);
        Assert.Contains("A2", healEntries[0].Message); // Healed the weakest
        Assert.Equal(20, healEntries[0].Data["amount"]);

        _output.WriteLine(trace.FormatTrace(verbose: true));

        TraceExtensions.CurrentTrace = null;
    }

    [Fact]
    public void Trace_Captures_Scope_Hierarchy()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1), [Keys.Hp] = 50 });
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
        });

        var trace = new SkillTrace(enabled: true);
        TraceExtensions.CurrentTrace = trace;

        var script = @"
            repeat 2 times {
                for each enemies within 3 do {
                    deal 5 damage to it
                }
            }
        ";
        var skill = TextDsl.FromTextUsingGlobals("MultiHit", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // Verify scopes were traced
        var scopeEntries = trace.Entries.Where(e => e.Type == "Scope").ToList();
        Assert.NotEmpty(scopeEntries);

        // Should have repeat scopes and iteration scopes
        Assert.Contains(scopeEntries, e => e.Message.Contains("repeat"));
        Assert.Contains(scopeEntries, e => e.Message.Contains("iteration"));

        _output.WriteLine(trace.FormatTrace(verbose: true));

        TraceExtensions.CurrentTrace = null;
    }
}
