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

    [Fact]
    public void DashTowards_MovesUnit()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1), [Keys.Speed] = 3 });
        s = WithUnit(s, "T", new Dictionary<string, object> { [Keys.Pos] = new Coord(5, 1) });

        var teams = new Dictionary<string, string> { ["C"] = "T1", ["T"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
                .SetItem(DslRuntime.CasterKey, "C")
                .SetItem(DslRuntime.TeamsKey, teams)
                .SetItem(DslRuntime.TargetKey, "T")
        });

        var script = "dash towards target up to 3";
        var skill = TextDsl.FromTextUsingGlobals("Dash", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        var newPos = (Coord)s.Units["C"].Vars[Keys.Pos];
        // Should have moved closer to target
        Assert.True(newPos.X > 1 || newPos.Y != 1);
    }

    [Fact]
    public void OrderBy_Var_Desc_SelectsHighestValue()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 2) });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 2), [Keys.Hp] = 10 });
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 2), [Keys.Hp] = 30 });
        s = WithUnit(s, "E3", new Dictionary<string, object> { [Keys.Pos] = new Coord(5, 2), [Keys.Hp] = 20 });

        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["E3"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
                .SetItem(DslRuntime.CasterKey, "C")
                .SetItem(DslRuntime.TeamsKey, teams)
        });

        var script = "for each enemies in range 100 of caster order by var \"hp\" desc limit 1 do { add tag \"diagnosed\" to it }";
        var skill = TextDsl.FromTextUsingGlobals("DiagnoseHighHP", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // Only E2 (highest HP) should have the tag
        Assert.False(s.Units["E1"].Tags.Contains("diagnosed"));
        Assert.True(s.Units["E2"].Tags.Contains("diagnosed"));
        Assert.False(s.Units["E3"].Tags.Contains("diagnosed"));
    }

    [Fact]
    public void ChanceCommand_SomeTimesExecutes()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "C") });

        var script = "chance 100% then { set global var \"hit\" = 1 } else { set global var \"miss\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("AlwaysHit", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        // 100% chance should always execute 'then' branch
        Assert.True(s.Global.Vars.ContainsKey("hit"));
        Assert.False(s.Global.Vars.ContainsKey("miss"));

        // Test 0% chance
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.Remove("hit").Remove("miss") });
        var script2 = "chance 0% then { set global var \"hit\" = 1 } else { set global var \"miss\" = 1 }";
        var skill2 = TextDsl.FromTextUsingGlobals("NeverHit", script2);
        (s, _) = se.ExecutePlan(s, skill2.BuildPlan(new Context(s)), validator: null);

        // 0% chance should always execute 'else' branch
        Assert.False(s.Global.Vars.ContainsKey("hit"));
        Assert.True(s.Global.Vars.ContainsKey("miss"));
    }

    [Fact]
    public void TagCheck_InConditional_Works()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        s = WithUnit(s, "T", new Dictionary<string, object> { [Keys.Hp] = 30 }, new[] { "dragon" });

        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
                .SetItem(DslRuntime.CasterKey, "C")
                .SetItem(DslRuntime.TargetKey, "T")
        });

        var script = "if target has tag \"dragon\" then { set global var \"is_dragon\" = 1 } else { set global var \"not_dragon\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("CheckDragon", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        Assert.True(s.Global.Vars.ContainsKey("is_dragon"));
        Assert.False(s.Global.Vars.ContainsKey("not_dragon"));
    }

    [Fact]
    public void ConsumeMp_ReducesMp()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Mp] = 10.0, [Keys.Pos] = new Coord(1, 1) });
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "C") });

        var script = "consume mp = 2.5";
        var skill = TextDsl.FromTextUsingGlobals("ConsumeMp", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        Assert.Equal(7.5, (double)s.Units["C"].Vars[Keys.Mp]);
    }

    // Exception tests
    [Fact]
    public void ETBBSException_CanBeCreated()
    {
        var ex1 = new ETBBSException();
        Assert.NotNull(ex1);

        var ex2 = new ETBBSException("test message");
        Assert.Equal("test message", ex2.Message);

        var inner = new Exception("inner");
        var ex3 = new ETBBSException("test", inner);
        Assert.Equal("test", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }

    [Fact]
    public void SkillValidationException_HasProperties()
    {
        var ex = new SkillValidationException("fireball", "out of range");
        Assert.Equal("fireball", ex.SkillName);
        Assert.Equal("out of range", ex.Reason);
        Assert.Contains("fireball", ex.Message);
        Assert.Contains("out of range", ex.Message);
    }

    [Fact]
    public void DslParseException_CanBeCreated()
    {
        var ex1 = new DslParseException("syntax error");
        Assert.Contains("syntax error", ex1.Message);
        Assert.Null(ex1.Line);
        Assert.Null(ex1.Column);

        var ex2 = new DslParseException("unexpected token", 5, 12);
        Assert.Equal(5, ex2.Line);
        Assert.Equal(12, ex2.Column);
        Assert.Contains("line 5", ex2.Message);
        Assert.Contains("column 12", ex2.Message);
    }

    [Fact]
    public void RoleLoadException_HasProperties()
    {
        var ex1 = new RoleLoadException("mage", "file not found");
        Assert.Equal("mage", ex1.RoleId);
        Assert.Contains("mage", ex1.Message);
        Assert.Contains("file not found", ex1.Message);

        var inner = new Exception("io error");
        var ex2 = new RoleLoadException("warrior", "parse failed", inner);
        Assert.Equal("warrior", ex2.RoleId);
        Assert.Same(inner, ex2.InnerException);
    }

    [Fact]
    public void StateCorruptionException_CanBeCreated()
    {
        var ex1 = new StateCorruptionException("state corrupted");
        Assert.Contains("state corrupted", ex1.Message);

        var inner = new Exception("corruption");
        var ex2 = new StateCorruptionException("invalid state", inner);
        Assert.Same(inner, ex2.InnerException);
    }

    [Fact]
    public void NetworkException_CanBeCreated()
    {
        var ex1 = new NetworkException("connection lost");
        Assert.Contains("connection lost", ex1.Message);

        var inner = new Exception("timeout");
        var ex2 = new NetworkException("send failed", inner);
        Assert.Same(inner, ex2.InnerException);
    }

    [Fact]
    public void ActionExecutionException_CanBeCreated()
    {
        var ex1 = new ActionExecutionException("execution failed");
        Assert.Contains("execution failed", ex1.Message);
        Assert.Null(ex1.Action);

        var action = new Move("U1", new Coord(1, 1));
        var ex2 = new ActionExecutionException(action, "move blocked");
        Assert.Same(action, ex2.Action);
        Assert.Contains("Move", ex2.Message);

        var inner = new Exception("collision");
        var ex3 = new ActionExecutionException(action, "collision detected", inner);
        Assert.Same(action, ex3.Action);
        Assert.Same(inner, ex3.InnerException);
    }

    // Context tests
    [Fact]
    public void Context_GetUnitVar_ReturnsDefaultWhenMissing()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U1", new Dictionary<string, object> { ["x"] = 42 });
        var ctx = new Context(s);

        Assert.Equal(42, ctx.GetUnitVar<int>("U1", "x", 0));
        Assert.Equal(0, ctx.GetUnitVar<int>("U1", "y", 0));
        Assert.Equal(99, ctx.GetUnitVar<int>("U2", "x", 99));
    }

    [Fact]
    public void Context_TryGetUnitVar_WorksCorrectly()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U1", new Dictionary<string, object> { ["name"] = "Alice" });
        var ctx = new Context(s);

        Assert.True(ctx.TryGetUnitVar<string>("U1", "name", out var name));
        Assert.Equal("Alice", name);

        Assert.False(ctx.TryGetUnitVar<string>("U1", "missing", out var missing));
        Assert.Null(missing);

        Assert.False(ctx.TryGetUnitVar<string>("U2", "name", out var noUnit));
    }

    [Fact]
    public void Context_GetTileVar_HandlesOutOfBounds()
    {
        var s = EmptyWorld(3, 3);
        s = WorldStateOps.WithTile(s, new Coord(1, 1), t => t with { Vars = t.Vars.SetItem("hazard", true) });
        var ctx = new Context(s);

        Assert.True(ctx.GetTileVar<bool>(new Coord(1, 1), "hazard", false));
        Assert.False(ctx.GetTileVar<bool>(new Coord(1, 1), "missing", false));
        Assert.False(ctx.GetTileVar<bool>(new Coord(-1, 0), "hazard", false));
        Assert.False(ctx.GetTileVar<bool>(new Coord(5, 5), "hazard", false));
    }

    [Fact]
    public void Context_TryGetTileVar_WorksCorrectly()
    {
        var s = EmptyWorld(3, 3);
        s = WorldStateOps.WithTile(s, new Coord(1, 1), t => t with { Vars = t.Vars.SetItem("cost", 5) });
        var ctx = new Context(s);

        Assert.True(ctx.TryGetTileVar<int>(new Coord(1, 1), "cost", out var cost));
        Assert.Equal(5, cost);

        Assert.False(ctx.TryGetTileVar<int>(new Coord(1, 1), "missing", out var missing));
        Assert.False(ctx.TryGetTileVar<int>(new Coord(-1, 0), "cost", out var oob));
    }

    [Fact]
    public void Context_HasTileTag_ChecksBounds()
    {
        var s = EmptyWorld(3, 3);
        s = WorldStateOps.WithTile(s, new Coord(1, 1), t => t with { Tags = t.Tags.Add("lava") });
        var ctx = new Context(s);

        Assert.True(ctx.HasTileTag(new Coord(1, 1), "lava"));
        Assert.False(ctx.HasTileTag(new Coord(0, 0), "lava"));
        Assert.False(ctx.HasTileTag(new Coord(-1, 0), "lava"));
        Assert.False(ctx.HasTileTag(new Coord(10, 10), "lava"));
    }

    [Fact]
    public void Context_GlobalVar_Methods()
    {
        var s = EmptyWorld();
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem("round", 3) });
        var ctx = new Context(s);

        Assert.Equal(3, ctx.GetGlobalVar<int>("round", 0));
        Assert.Equal(0, ctx.GetGlobalVar<int>("missing", 0));

        Assert.True(ctx.TryGetGlobalVar<int>("round", out var round));
        Assert.Equal(3, round);
        Assert.False(ctx.TryGetGlobalVar<int>("missing", out var missing));
    }

    [Fact]
    public void Context_HasGlobalTag_Works()
    {
        var s = EmptyWorld();
        s = WorldStateOps.WithGlobal(s, g => g with { Tags = g.Tags.Add("night") });
        var ctx = new Context(s);

        Assert.True(ctx.HasGlobalTag("night"));
        Assert.False(ctx.HasGlobalTag("day"));
    }

    [Fact]
    public void Context_TryGetUnitPos_Works()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U1", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 4) });
        var ctx = new Context(s);

        Assert.True(ctx.TryGetUnitPos("U1", out var pos));
        Assert.Equal(new Coord(3, 4), pos);

        Assert.False(ctx.TryGetUnitPos("U2", out var noPos));
    }

    [Fact]
    public void Context_GetUnitPosOrDefault_Works()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 3) });
        var ctx = new Context(s);

        Assert.Equal(new Coord(2, 3), ctx.GetUnitPosOrDefault("U1"));
        Assert.Equal(default(Coord), ctx.GetUnitPosOrDefault("U2"));
        Assert.Equal(new Coord(5, 5), ctx.GetUnitPosOrDefault("U2", new Coord(5, 5)));
    }

    // Selection tests
    [Fact]
    public void Selection_ById_ReturnsSpecifiedId()
    {
        var s = EmptyWorld();
        var ctx = new Context(s);
        var selector = Selection.ById("target123");
        Assert.Equal("target123", selector(ctx));
    }

    [Fact]
    public void Selection_UnitsWithTag_FiltersCorrectly()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U1", new Dictionary<string, object>(), new[] { "poisoned" });
        s = WithUnit(s, "U2", new Dictionary<string, object>(), new[] { "stunned" });
        s = WithUnit(s, "U3", new Dictionary<string, object>(), new[] { "poisoned" });
        var ctx = new Context(s);

        var selector = Selection.UnitsWithTag("poisoned");
        var result = selector(ctx).ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains("U1", result);
        Assert.Contains("U3", result);
    }

    [Fact]
    public void Selection_Allies_FiltersCorrectly()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A1", new Dictionary<string, object>());
        s = WithUnit(s, "A2", new Dictionary<string, object>());
        s = WithUnit(s, "E1", new Dictionary<string, object>());
        var teams = new Dictionary<string, string> { ["A1"] = "team1", ["A2"] = "team1", ["E1"] = "team2" };
        var ctx = new Context(s);

        var selector = Selection.Allies("A1", teams);
        var result = selector(ctx).ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains("A1", result);
        Assert.Contains("A2", result);
    }

    [Fact]
    public void Selection_Enemies_FiltersCorrectly()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A1", new Dictionary<string, object>());
        s = WithUnit(s, "E1", new Dictionary<string, object>());
        s = WithUnit(s, "E2", new Dictionary<string, object>());
        var teams = new Dictionary<string, string> { ["A1"] = "team1", ["E1"] = "team2", ["E2"] = "team2" };
        var ctx = new Context(s);

        var selector = Selection.Enemies("A1", teams);
        var result = selector(ctx).ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains("E1", result);
        Assert.Contains("E2", result);
    }

    [Fact]
    public void Selection_WithinRange_Manhattan()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 2) });
        s = WithUnit(s, "U1", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 2) }); // d=1
        s = WithUnit(s, "U2", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 4) }); // d=2
        s = WithUnit(s, "U3", new Dictionary<string, object> { [Keys.Pos] = new Coord(5, 5) }); // d=6
        var ctx = new Context(s);

        var selector = Selection.WithinRange("C", 2, DistanceMetric.Manhattan);
        var result = selector(ctx).ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains("U1", result);
        Assert.Contains("U2", result);
    }

    [Fact]
    public void Selection_EnemiesWithinRange_Works()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 2) });
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 2) }); // ally, close
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 3) }); // enemy, close
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(5, 5) }); // enemy, far
        var teams = new Dictionary<string, string> { ["C"] = "t1", ["A"] = "t1", ["E1"] = "t2", ["E2"] = "t2" };
        var ctx = new Context(s);

        var selector = Selection.EnemiesWithinRange("C", teams, 2);
        var result = selector(ctx).ToList();
        Assert.Single(result);
        Assert.Contains("E1", result);
    }

    [Fact]
    public void Selection_SortByNearestTo_Works()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 2) });
        s = WithUnit(s, "U1", new Dictionary<string, object> { [Keys.Pos] = new Coord(5, 5) }); // d=6
        s = WithUnit(s, "U2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 2) }); // d=1
        s = WithUnit(s, "U3", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 4) }); // d=2
        var ctx = new Context(s);

        var selector = Selection.SortByNearestTo("C");
        var result = selector(ctx).ToList();
        Assert.Equal("U2", result[0]);
        Assert.Equal("U3", result[1]);
        Assert.Equal("U1", result[2]);
    }

    [Fact]
    public void Selection_OrderByUnitVarInt_Ascending()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U1", new Dictionary<string, object> { [Keys.Hp] = 30 });
        s = WithUnit(s, "U2", new Dictionary<string, object> { [Keys.Hp] = 10 });
        s = WithUnit(s, "U3", new Dictionary<string, object> { [Keys.Hp] = 20 });
        var ctx = new Context(s);

        var selector = Selection.OrderByUnitVarInt(Keys.Hp, ascending: true);
        var result = selector(ctx).ToList();
        Assert.Equal("U2", result[0]);
        Assert.Equal("U3", result[1]);
        Assert.Equal("U1", result[2]);
    }

    [Fact]
    public void Selection_OrderByUnitVarInt_Descending()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U1", new Dictionary<string, object> { [Keys.Hp] = 30 });
        s = WithUnit(s, "U2", new Dictionary<string, object> { [Keys.Hp] = 10 });
        s = WithUnit(s, "U3", new Dictionary<string, object> { [Keys.Hp] = 20 });
        var ctx = new Context(s);

        var selector = Selection.OrderByUnitVarInt(Keys.Hp, ascending: false);
        var result = selector(ctx).ToList();
        Assert.Equal("U1", result[0]);
        Assert.Equal("U3", result[1]);
        Assert.Equal("U2", result[2]);
    }

    [Fact]
    public void Selection_LowestHp_Works()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U1", new Dictionary<string, object> { [Keys.Hp] = 30 });
        s = WithUnit(s, "U2", new Dictionary<string, object> { [Keys.Hp] = 5 });
        s = WithUnit(s, "U3", new Dictionary<string, object> { [Keys.Hp] = 20 });
        var ctx = new Context(s);

        var selector = Selection.LowestHp();
        var result = selector(ctx).ToList();
        Assert.Equal("U2", result[0]);
    }
}

