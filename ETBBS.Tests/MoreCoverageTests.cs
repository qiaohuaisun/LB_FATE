using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ETBBS;
using Xunit;

public class MoreCoverageTests
{
    private static WorldState EmptyWorld(int w = 8, int h = 8) => WorldState.CreateEmpty(w, h);
    private static WorldState WithUnit(WorldState s, string id, IDictionary<string, object> vars, IEnumerable<string>? tags = null)
    {
        var imVars = ImmutableDictionary.CreateRange(vars);
        var imTags = tags is null ? ImmutableHashSet<string>.Empty : ImmutableHashSet.CreateRange(tags);
        return WorldStateOps.WithUnit(s, id, _ => new UnitState(imVars, imTags));
    }

    [Fact]
    public void Validator_SealedUntil_Day_Phase_Works()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "C") });
        var script = "sealed_until day 3 phase 2; set global var \"ok\" = 1";
        var skill = TextDsl.FromTextUsingGlobals("SealDP", script);
        var se = new SkillExecutor();

        // Before unlock: day 3 phase 1 -> blocked
        var cfgLocked = new ActionValidationConfig(CasterId: "C", CurrentDay: 3, CurrentPhase: 1);
        var valLocked = ActionValidators.ForSkillWithExtras(skill, cfgLocked, cooldownStore: null);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valLocked);
        Assert.False(s.Global.Vars.ContainsKey("ok"));

        // At unlock: day 3 phase 2 -> allowed
        var cfgOpen = cfgLocked with { CurrentPhase = 2 };
        var valOpen = ActionValidators.ForSkillWithExtras(skill, cfgOpen, cooldownStore: null);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valOpen);
        Assert.Equal(1, (int)Convert.ToInt32(s.Global.Vars["ok"]));
    }

    [Fact]
    public void Validator_Range_Chebyshev_And_Euclidean()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        var script = "range 1; targeting tile; set global var \"hit\" = 1";
        var skill = TextDsl.FromTextUsingGlobals("TileRange1", script);
        var se = new SkillExecutor();

        // Manhattan (default): (2,2) is d=2 -> blocked
        var cfgM = new ActionValidationConfig(CasterId: "C", TargetPos: new Coord(2, 2));
        var valM = ActionValidators.ForSkillWithExtras(skill, cfgM, null);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valM);
        Assert.False(s.Global.Vars.ContainsKey("hit"));

        // Chebyshev: d=1 -> allowed
        var cfgC = cfgM with { DistanceMetric = DistanceMetric.Chebyshev };
        var valC = ActionValidators.ForSkillWithExtras(skill, cfgC, null);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valC);
        Assert.Equal(1, (int)Convert.ToInt32(s.Global.Vars["hit"]));

        // Clear and test Euclidean: sqrt(2) ~= 1.41 -> rounds to 1 -> allowed
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.Remove("hit") });
        var cfgE = cfgM with { DistanceMetric = DistanceMetric.Euclidean };
        var valE = ActionValidators.ForSkillWithExtras(skill, cfgE, null);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valE);
        Assert.Equal(1, (int)Convert.ToInt32(s.Global.Vars["hit"]));
    }

    [Fact]
    public void EvadeCharges_Grants_NextAttackMultiplier()
    {
        var s = EmptyWorld();
        // A attacks B; B has one evade charge
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Atk] = 5, [Keys.Hp] = 40, [Keys.Def] = 0, [Keys.Pos] = new Coord(0, 0) });
        s = WithUnit(s, "B", new Dictionary<string, object> { [Keys.Hp] = 40, [Keys.Def] = 0, [Keys.EvadeCharges] = 1, [Keys.Pos] = new Coord(0, 1), [Keys.Atk] = 5 });

        // A -> B physical: should be evaded; B gains next_attack_multiplier
        s = new PhysicalDamage("A", "B", Power: 5).Compile()(s);
        Assert.Equal(40, (int)s.Units["B"].Vars[Keys.Hp]);
        Assert.True(s.Units["B"].Vars.TryGetValue(Keys.NextAttackMultiplier, out var multObj) && Convert.ToDouble(multObj) >= 2.0);

        // B -> A physical: should consume multiplier and deal double
        // Without multiplier raw = 5 + 5 - 0 = 10; double => 20
        s = new PhysicalDamage("B", "A", Power: 5).Compile()(s);
        Assert.Equal(20, (int)s.Units["A"].Vars[Keys.Hp]);
        Assert.False(s.Units["B"].Vars.ContainsKey(Keys.NextAttackMultiplier));
    }

    [Fact]
    public void Heal_Reversed_By_Global_And_NoHeal_Blocks()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U", new Dictionary<string, object> { [Keys.Hp] = 20, [Keys.MaxHp] = 50 });

        // Reverse heal: set global reverse flag -> heal 5 becomes damage 5
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(Keys.ReverseHealTurnsGlobal, 1) });
        s = new Heal("U", 5).Compile()(s);
        Assert.Equal(15, (int)s.Units["U"].Vars[Keys.Hp]);

        // No-heal status: heal should be ignored
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(Keys.ReverseHealTurnsGlobal, 0) });
        s = WorldStateOps.WithUnit(s, "U", u => u with { Vars = u.Vars.SetItem(Keys.NoHealTurns, 1) });
        s = new Heal("U", 10).Compile()(s);
        Assert.Equal(15, (int)s.Units["U"].Vars[Keys.Hp]);
    }

    [Fact]
    public void Events_TileVar_And_Tags_Are_Published()
    {
        var s = EmptyWorld();
        var bus = new EventBus();
        int tileVar = 0; int unitTagAdd = 0; int unitTagRemove = 0; int globTagAdd = 0; int globTagRemove = 0;
        using var d1 = bus.Subscribe(EventTopics.TileVarChanged, _ => tileVar++);
        using var d2 = bus.Subscribe(EventTopics.UnitTagAdded, _ => unitTagAdd++);
        using var d3 = bus.Subscribe(EventTopics.UnitTagRemoved, _ => unitTagRemove++);
        using var d4 = bus.Subscribe(EventTopics.GlobalTagAdded, _ => globTagAdd++);
        using var d5 = bus.Subscribe(EventTopics.GlobalTagRemoved, _ => globTagRemove++);

        var se = new SkillExecutor();
        var actions = new AtomicAction[]
        {
            new SetTileVar(new Coord(2,2), "hazard", true),
            new AddUnitTag("U", Tags.Stunned),
            new RemoveUnitTag("U", Tags.Stunned),
            new AddGlobalTag("x"),
            new RemoveGlobalTag("x"),
        };
        (s, _) = se.Execute(s, actions, events: bus);
        Assert.Equal(1, tileVar);
        Assert.Equal(1, unitTagAdd);
        Assert.Equal(1, unitTagRemove);
        Assert.Equal(1, globTagAdd);
        Assert.Equal(1, globTagRemove);
    }

    [Fact]
    public void ForEach_Range_From_Point_Selector()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 2) });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 2) }); // within range from point (3,2)
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(6, 2) }); // outside
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
                .SetItem(DslRuntime.CasterKey, "C")
                .SetItem(DslRuntime.TeamsKey, teams)
                .SetItem(DslRuntime.TargetPointKey, new Coord(3, 2))
        });
        var script = "for each enemies in range 2 of point do { set unit(it) var \"mark\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("SelPoint", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);
        Assert.Equal(1, (int)Convert.ToInt32(s.Units["E1"].Vars["mark"]));
        Assert.False(s.Units["E2"].Vars.ContainsKey("mark"));
    }
}

