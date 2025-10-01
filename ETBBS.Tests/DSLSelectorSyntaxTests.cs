using ETBBS;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1) }); // d=1
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1) }); // d=2
        s = WithUnit(s, "E3", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 1) }); // d=3
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["E3"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
        });

        var script = "for each nearest 2 enemies of caster do { set unit(it) var \"near\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("Nearest2Enemies", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        Assert.Equal(1, (int)Convert.ToInt32(s.Units["E1"].Vars["near"]));
        Assert.Equal(1, (int)Convert.ToInt32(s.Units["E2"].Vars["near"]));
        Assert.DoesNotContain("near", s.Units["E3"].Vars.Keys);
    }

    [Fact]
    public void Farthest_1_Allies_Of_Point()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        s = WithUnit(s, "A1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1) }); // d=1 from point
        s = WithUnit(s, "A2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1) }); // d=2
        s = WithUnit(s, "A3", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 1) }); // d=3
        var teams = new Dictionary<string, string> { ["C"] = "T", ["A1"] = "T", ["A2"] = "T", ["A3"] = "T" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
            .SetItem(DslRuntime.TargetPointKey, new Coord(1, 1))
        });

        var script = "for each farthest 1 allies of point do { set unit(it) var \"far\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("Farthest1AlliesOfPoint", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        Assert.Equal(1, (int)Convert.ToInt32(s.Units["A3"].Vars["far"]));
        Assert.DoesNotContain("far", s.Units["A1"].Vars.Keys);
        Assert.DoesNotContain("far", s.Units["A2"].Vars.Keys);
    }

    [Fact]
    public void Random_2_Enemies_Selects_Two()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1) });
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1) });
        s = WithUnit(s, "E3", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 1) });
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["E3"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
            .SetItem(DslRuntime.RngKey, new Random(42)) // Fixed seed for determinism
        });

        var script = "for each random 2 enemies do { set unit(it) var \"selected\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("Random2Enemies", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // Exactly 2 should be selected
        int count = 0;
        if (s.Units["E1"].Vars.ContainsKey("selected")) count++;
        if (s.Units["E2"].Vars.ContainsKey("selected")) count++;
        if (s.Units["E3"].Vars.ContainsKey("selected")) count++;
        Assert.Equal(2, count);
    }

    [Fact]
    public void Healthiest_Enemies_Selects_Highest_HP()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1), [Keys.Hp] = 100 });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1), [Keys.Hp] = 30 });
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1), [Keys.Hp] = 80 }); // highest
        s = WithUnit(s, "E3", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 1), [Keys.Hp] = 50 });
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["E3"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
        });

        var script = "for each healthiest enemies do { set unit(it) var \"targeted\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("HealthiestEnemy", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // Only E2 (highest HP) should be selected
        Assert.DoesNotContain("targeted", s.Units["E1"].Vars.Keys);
        Assert.Equal(1, (int)Convert.ToInt32(s.Units["E2"].Vars["targeted"]));
        Assert.DoesNotContain("targeted", s.Units["E3"].Vars.Keys);
    }

    [Fact]
    public void Weakest_2_Enemies_Selects_Lowest_HP()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1), [Keys.Hp] = 100 });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1), [Keys.Hp] = 30 }); // 2nd lowest
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1), [Keys.Hp] = 80 });
        s = WithUnit(s, "E3", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 1), [Keys.Hp] = 10 }); // lowest
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["E3"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
        });

        var script = "for each weakest 2 enemies do { set unit(it) var \"targeted\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("Weakest2Enemies", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // E1 and E3 (lowest HP) should be selected
        Assert.Equal(1, (int)Convert.ToInt32(s.Units["E1"].Vars["targeted"]));
        Assert.DoesNotContain("targeted", s.Units["E2"].Vars.Keys);
        Assert.Equal(1, (int)Convert.ToInt32(s.Units["E3"].Vars["targeted"]));
    }
}
