using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ETBBS;
using Xunit;

public class DSLSelectorSyntaxTests
{
    private static WorldState EmptyWorld(int w = 10, int h = 10)
        => WorldState.CreateEmpty(w, h);

    private static WorldState WithUnit(WorldState s, string id, IDictionary<string, object> vars)
    {
        var imVars = ImmutableDictionary.CreateRange(vars);
        return WorldStateOps.WithUnit(s, id, _ => new UnitState(imVars, ImmutableHashSet<string>.Empty));
    }

    [Fact]
    public void Nearest_Enemies_Of_Caster_Limit2()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1,1) });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2,1) }); // d=1
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3,1) }); // d=2
        s = WithUnit(s, "E3", new Dictionary<string, object> { [Keys.Pos] = new Coord(4,1) }); // d=3
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["E3"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
        });

        var script = "for each nearest 2 enemies of caster do { set unit(it) var \"near\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("Nearest2Enemies", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        Assert.Equal(1, (int)Convert.ToInt32(s.Units["E1"].Vars["near"]));
        Assert.Equal(1, (int)Convert.ToInt32(s.Units["E2"].Vars["near"]));
        Assert.False(s.Units["E3"].Vars.ContainsKey("near"));
    }

    [Fact]
    public void Farthest_1_Allies_Of_Point()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1,1) });
        s = WithUnit(s, "A1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2,1) }); // d=1 from point
        s = WithUnit(s, "A2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3,1) }); // d=2
        s = WithUnit(s, "A3", new Dictionary<string, object> { [Keys.Pos] = new Coord(4,1) }); // d=3
        var teams = new Dictionary<string, string> { ["C"] = "T", ["A1"] = "T", ["A2"] = "T", ["A3"] = "T" };
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
            .SetItem(DslRuntime.TargetPointKey, new Coord(1,1))
        });

        var script = "for each farthest 1 allies of point do { set unit(it) var \"far\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("Farthest1AlliesOfPoint", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        Assert.Equal(1, (int)Convert.ToInt32(s.Units["A3"].Vars["far"]));
        Assert.False(s.Units["A1"].Vars.ContainsKey("far"));
        Assert.False(s.Units["A2"].Vars.ContainsKey("far"));
    }
}

