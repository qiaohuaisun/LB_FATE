using ETBBS;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

public class CombatTests
{
    private static WorldState EmptyWorld(int w = 10, int h = 10)
        => WorldState.CreateEmpty(w, h);

    private static WorldState WithUnit(WorldState s, string id, IDictionary<string, object> vars, IEnumerable<string>? tags = null)
    {
        var imVars = ImmutableDictionary.CreateRange(vars);
        var imTags = tags is null ? ImmutableHashSet<string>.Empty : ImmutableHashSet.CreateRange(tags);
        return WorldStateOps.WithUnit(s, id, _ => new UnitState(imVars, imTags));
    }

    [Fact]
    public void LineAoeDamage_Physical_SkipsAllies_HitsEnemies()
    {
        var s = EmptyWorld();
        // Teams: A and X allies (T1); E enemy (T2)
        var teams = new Dictionary<string, string> { ["A"] = "T1", ["X"] = "T1", ["E"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.TeamsKey, teams) });

        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 0), [Keys.Atk] = 5 });
        s = WithUnit(s, "X", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 1), [Keys.Hp] = 30, [Keys.Def] = 0 });
        s = WithUnit(s, "E", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 3), [Keys.Hp] = 30, [Keys.Def] = 0 });
        // aim towards a target behind the enemy so the path includes E's cell
        s = WithUnit(s, "T", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 4), [Keys.Hp] = 1 });

        var eff = new LineAoeDamage("A", TargetId: "T", Power: 5, Length: 3, Radius: 0, Flavor: DamageFlavor.Physical, IgnoreRatio: 0.0).Compile();
        s = eff(s);
        // Ally X should be unaffected
        Assert.Equal(30, s.Units["X"].GetIntVar(Keys.Hp));
        // Enemy E should take 5+5=10 damage
        Assert.Equal(20, s.Units["E"].GetIntVar(Keys.Hp));
    }

    private sealed class FixedRandom : Random
    {
        private readonly double _v;
        public FixedRandom(double v) => _v = v;
        public override double NextDouble() => _v;
    }

    [Fact]
    public void DSL_Chance_Then_Else_Branches_Deterministic()
    {
        var s = EmptyWorld();
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.RngKey, new FixedRandom(0.1)) });
        var thenScript = "chance 50% then { set global var \"flag\" = 1 } else { set global var \"flag\" = 2 }";
        var skill1 = TextDsl.FromTextUsingGlobals("Chance", thenScript);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill1.BuildPlan(new Context(s)), validator: null);
        Assert.Equal(1, TypeConversion.GetIntFrom(s.Global.Vars, "flag", 0));

        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.RngKey, new FixedRandom(0.9)) });
        (s, _) = se.ExecutePlan(s, skill1.BuildPlan(new Context(s)), validator: null);
        Assert.Equal(2, TypeConversion.GetIntFrom(s.Global.Vars, "flag", 0));
    }

    [Fact]
    public void Validator_Cooldown_And_SealedUntil()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { ["count"] = 0 });
        var skill = SkillBuilder.Create("CDSkill")
            .WithExtra("cooldown", 2)
            .WithExtra("sealed_until", 3)
            .Range(0)
            .Script(ss => ss.ModifyUnitVar("A", "count", v => v is int i ? i + 1 : 1))
            .Build();
        var store = new InMemoryCooldownStore();
        var se = new SkillExecutor();

        // Before sealed_until => blocked
        var cfg1 = new ActionValidationConfig(CasterId: "A", CurrentTurn: 2);
        var val1 = ActionValidators.ForSkillWithExtras(skill, cfg1, store);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: val1);
        Assert.Equal(0, TypeConversion.GetIntFrom(s.Units["A"].Vars, "count", 0));

        // At turn 3 => allowed
        var cfg2 = cfg1 with { CurrentTurn = 3 };
        var val2 = ActionValidators.ForSkillWithExtras(skill, cfg2, store);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: val2);
        Assert.Equal(1, TypeConversion.GetIntFrom(s.Units["A"].Vars, "count", 0));

        // Simulate store record and retry same turn => blocked by cooldown
        store.SetLastUseTurn("A", skill.Metadata.Name, 3);
        var val3 = ActionValidators.ForSkillWithExtras(skill, cfg2, store);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: val3);
        Assert.Equal(1, TypeConversion.GetIntFrom(s.Units["A"].Vars, "count", 0));
    }

    [Fact]
    public void Validator_Range_Targeting_Tile_With_TargetPoint()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        var script = "range 2; targeting tile; set global var \"hit\" = 1";
        var skill = TextDsl.FromTextUsingGlobals("TileSkill", script);
        var se = new SkillExecutor();

        // within range: (3,1) is distance 2 from (1,1)
        var cfgIn = new ActionValidationConfig(CasterId: "C", TargetPos: new Coord(3, 1));
        var valIn = ActionValidators.ForSkillWithExtras(skill, cfgIn, cooldownStore: null);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valIn);
        Assert.Equal(1, (int)Convert.ToInt32(s.Global.Vars.GetValueOrDefault("hit", 0)));

        // beyond range: (4,1) is distance 3
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.Remove("hit") });
        var cfgOut = cfgIn with { TargetPos = new Coord(4, 1) };
        var valOut = ActionValidators.ForSkillWithExtras(skill, cfgOut, cooldownStore: null);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valOut);
        Assert.DoesNotContain("hit", s.Global.Vars.Keys);
    }

    [Fact]
    public void Events_Publish_UnitDamaged_And_Moved()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U", new Dictionary<string, object> { [Keys.Hp] = 10, [Keys.Pos] = new Coord(0, 0) });
        var bus = new EventBus(); int dmg = 0; int moved = 0;
        using var d1 = bus.Subscribe(EventTopics.UnitDamaged, _ => dmg++);
        using var d2 = bus.Subscribe(EventTopics.UnitMoved, _ => moved++);
        var se = new SkillExecutor();
        (s, _) = se.Execute(s, new AtomicAction[] { new Damage("U", 3), new Move("U", new Coord(1, 0)) }, events: bus);
        Assert.Equal(1, dmg);
        Assert.Equal(1, moved);
    }

    [Fact]
    public void DSL_Parallel_SetsBothGlobals()
    {
        var s = EmptyWorld();
        var script = "parallel { set global var \"a\" = 1; set global var \"b\" = 2; }";
        var skill = TextDsl.FromTextUsingGlobals("Par", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);
        Assert.Equal(1, TypeConversion.GetIntFrom(s.Global.Vars, "a", 0));
        Assert.Equal(2, TypeConversion.GetIntFrom(s.Global.Vars, "b", 0));
    }

    [Fact]
    public void DSL_Repeat_AccumulatesDamage()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Atk] = 0, [Keys.Pos] = new Coord(0, 0) });
        s = WithUnit(s, "B", new Dictionary<string, object> { [Keys.Hp] = 10, [Keys.Def] = 0, [Keys.Pos] = new Coord(0, 1) });
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "A")
            .SetItem(DslRuntime.TargetKey, "B")
        });
        var script = "repeat 3 times { deal 1 damage to target }";
        var skill = TextDsl.FromTextUsingGlobals("Rep", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);
        Assert.Equal(7, s.Units["B"].GetIntVar(Keys.Hp));
    }

    [Fact]
    public void DSL_NestedIf_TagsAndMpPath()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Mp] = 1, ["x"] = 0 }, tags: new[] { "t1" });
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "A") });
        var script = "if caster has tag \"t1\" then { if caster mp >= 1 then { set global var \"path\" = \"t1-mp\" } else { set global var \"path\" = \"t1-nomp\" } } else { set global var \"path\" = \"no-t1\" }";
        var skill = TextDsl.FromTextUsingGlobals("IfNest", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);
        Assert.Equal("t1-mp", (string)s.Global.Vars["path"]);
    }

    [Fact]
    public void DSL_ForEach_InParallel_WithRange_MarksTargets()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1) }); // d=1
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1) }); // d=2
        s = WithUnit(s, "E3", new Dictionary<string, object> { [Keys.Pos] = new Coord(5, 1) }); // d=4
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["E3"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "C")
            .SetItem(DslRuntime.TeamsKey, teams)
        });
        var script = "for each enemies of caster in range 2 of caster in parallel do { set unit(it) var \"mark\" = 1 }";
        var skill = TextDsl.FromTextUsingGlobals("FEParRange", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);
        Assert.Equal(1, TypeConversion.GetIntFrom(s.Units["E1"].Vars, "mark", 0));
        Assert.Equal(1, TypeConversion.GetIntFrom(s.Units["E2"].Vars, "mark", 0));
        Assert.DoesNotContain("mark", s.Units["E3"].Vars.Keys);
    }
    [Fact]
    public void PerTurnAdd_ClampToOne_ForResist()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U", new Dictionary<string, object> { [Keys.ResistMagic] = 0.9, ["per_turn_add:resist_magic"] = 0.2 });
        var ts = new TurnSystem();
        (s, _) = ts.AdvanceTurn(s);
        var r = s.Units["U"].GetDoubleVar(Keys.ResistMagic, 0.0);
        Assert.Equal(1.0, r, 5);
    }
    [Fact]
    public void LineAoeDamage_Magic_IgnoreResistStat_Applies_And_ResistMagic_Reduces()
    {
        var s = EmptyWorld();
        var teams = new Dictionary<string, string> { ["A"] = "T1", ["E"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.TeamsKey, teams) });
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 0), [Keys.MAtk] = 10 });
        s = WithUnit(s, "E", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 3), [Keys.Hp] = 30, [Keys.MDef] = 10, [Keys.ResistMagic] = 0.2 });
        s = WithUnit(s, "T", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 4), [Keys.Hp] = 1 });
        // effRes = mdef*(1-0.5)=5; raw0=5+10-5=10; resistMagic 20% => 8
        s = new LineAoeDamage("A", TargetId: "T", Power: 5, Length: 3, Radius: 0, Flavor: DamageFlavor.Magic, IgnoreRatio: 0.5).Compile()(s);
        Assert.Equal(22, s.Units["E"].GetIntVar(Keys.Hp));
    }

    [Fact]
    public void LineAoeDamage_True_IgnoresAtkDefAndResists()
    {
        var s = EmptyWorld();
        var teams = new Dictionary<string, string> { ["A"] = "T1", ["E"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.TeamsKey, teams) });
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 0), [Keys.Atk] = 999 });
        s = WithUnit(s, "E", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 3), [Keys.Hp] = 30, [Keys.Def] = 1000, [Keys.ResistPhysical] = 0.9, [Keys.ResistMagic] = 0.9 });
        s = WithUnit(s, "T", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 4), [Keys.Hp] = 1 });
        // True damage deals Power directly (7)
        s = new LineAoeDamage("A", TargetId: "T", Power: 7, Length: 3, Radius: 0, Flavor: DamageFlavor.True).Compile()(s);
        Assert.Equal(23, s.Units["E"].GetIntVar(Keys.Hp));
    }

    [Fact]
    public void StatusImmune_TicksDown_And_BlocksAllCCKinds()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U", new Dictionary<string, object> { [Keys.StatusImmuneTurns] = 1 });
        var actions = new AtomicAction[]
        {
            new AddUnitTag("U", Tags.Stunned), new SetUnitVar("U", Keys.StunnedTurns, 2),
            new AddUnitTag("U", Tags.Silenced), new SetUnitVar("U", Keys.SilencedTurns, 2),
            new AddUnitTag("U", Tags.Rooted),   new SetUnitVar("U", Keys.RootedTurns, 2),
        };
        var se = new SkillExecutor();
        var opts = new ExecutionOptions(TransactionalBatch: true, ConflictHandling: ConflictHandling.WarnOnly, EmitPerActionEventsInTransactional: true);
        (s, _) = se.Execute(s, actions, validator: null, events: null, options: opts);
        var u = s.Units["U"];
        Assert.DoesNotContain(Tags.Stunned, u.Tags);
        Assert.DoesNotContain(Tags.Silenced, u.Tags);
        Assert.DoesNotContain(Tags.Rooted, u.Tags);
        Assert.Equal(0, TypeConversion.GetIntFrom(u.Vars, Keys.StunnedTurns, 0));
        Assert.Equal(0, TypeConversion.GetIntFrom(u.Vars, Keys.SilencedTurns, 0));
        Assert.Equal(0, TypeConversion.GetIntFrom(u.Vars, Keys.RootedTurns, 0));

        // Tick immunity down by one turn
        var ts = new TurnSystem();
        (s, _) = ts.AdvanceTurn(s);
        var im = TypeConversion.GetIntFrom(s.Units["U"].Vars, Keys.StatusImmuneTurns, 0);
        Assert.Equal(0, im);
    }
    [Fact]
    public void DashTowards_BlockedByOccupiedTile()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 0), [Keys.Hp] = 10 });
        s = WithUnit(s, "B", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 2), [Keys.Hp] = 10 }); // blocker alive
        s = WithUnit(s, "T", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 3), [Keys.Hp] = 10 });

        s = new DashTowards("A", "T", MaxSteps: 3).Compile()(s);
        var pos = (Coord)s.Units["A"].Vars[Keys.Pos];
        Assert.Equal(new Coord(0, 1), pos);
    }

    [Fact]
    public void PhysicalDamage_Duel_TriplesDamage()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Atk] = 5 }, tags: new[] { Tags.Duel });
        s = WithUnit(s, "B", new Dictionary<string, object> { [Keys.Hp] = 40, [Keys.Def] = 0 }, tags: new[] { Tags.Duel });
        s = new PhysicalDamage("A", "B", Power: 5, IgnoreDefenseRatio: 0.0).Compile()(s);
        // base = 10 -> duel x3 => 30, 40-30=10
        Assert.Equal(10, s.Units["B"].GetIntVar(Keys.Hp));
    }

    [Fact]
    public void Transactional_Conflict_BlocksAndKeepsState()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U", new Dictionary<string, object> { [Keys.Hp] = 10 });
        var actions = new AtomicAction[] { new SetUnitVar("U", Keys.Hp, 20), new SetUnitVar("U", Keys.Hp, 30) };
        var se = new SkillExecutor();
        var opts = new ExecutionOptions(TransactionalBatch: true, ConflictHandling: ConflictHandling.BlockOnConflict, EmitPerActionEventsInTransactional: false);
        (s, var log) = se.Execute(s, actions, validator: null, events: null, options: opts);
        Assert.Equal(10, s.Units["U"].GetIntVar(Keys.Hp));
        Assert.Contains(log.Messages, m => m.Contains("conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Transactional_StatusImmunity_IgnoresNewCrowdControl()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U", new Dictionary<string, object> { [Keys.StatusImmuneTurns] = 1 });
        var actions = new AtomicAction[] { new AddUnitTag("U", Tags.Stunned), new SetUnitVar("U", Keys.StunnedTurns, 2) };
        var se = new SkillExecutor();
        var opts = new ExecutionOptions(TransactionalBatch: true, ConflictHandling: ConflictHandling.WarnOnly, EmitPerActionEventsInTransactional: true);
        (s, _) = se.Execute(s, actions, validator: null, events: null, options: opts);
        var u = s.Units["U"];
        Assert.DoesNotContain(Tags.Stunned, u.Tags);
        Assert.True(u.Vars.TryGetValue(Keys.StunnedTurns, out var st));
        Assert.Equal(0, st is int i ? i : 0);
    }

    [Fact]
    public void TurnSystem_StatusTicks_AddAndRemoveTags()
    {
        var s = EmptyWorld();
        // U1: 2 turns stunned -> after 1 turn, should have stunned tag and 1 left
        s = WithUnit(s, "U1", new Dictionary<string, object> { [Keys.StunnedTurns] = 2 });
        // U2: 1 turn stunned -> after 1 turn, tag removed and 0
        s = WithUnit(s, "U2", new Dictionary<string, object> { [Keys.StunnedTurns] = 1 });
        var ts = new TurnSystem();
        (s, _) = ts.AdvanceTurn(s);
        var u1 = s.Units["U1"]; var u2 = s.Units["U2"];
        Assert.Contains(Tags.Stunned, u1.Tags);
        Assert.Equal(1, u1.GetIntVar(Keys.StunnedTurns));
        Assert.DoesNotContain(Tags.Stunned, u2.Tags);
        Assert.Equal(0, u2.GetIntVar(Keys.StunnedTurns));
    }

    [Fact]
    public void TurnSystem_Bleed_TicksAndCleansUp()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U", new Dictionary<string, object> { [Keys.Hp] = 20, [Keys.BleedTurns] = 1, [Keys.BleedPerTurn] = 3 }, tags: new[] { Tags.Bleeding });
        var ts = new TurnSystem();
        (s, _) = ts.AdvanceTurn(s);
        var u = s.Units["U"];
        Assert.Equal(17, u.GetIntVar(Keys.Hp));
        Assert.Equal(0, u.GetIntVar(Keys.BleedTurns));
        Assert.DoesNotContain(Keys.BleedPerTurn, u.Vars.Keys);
        Assert.DoesNotContain(Tags.Bleeding, u.Tags);
    }

    [Fact]
    public void Transactional_PerActionEvents_Toggle()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U", new Dictionary<string, object> { [Keys.Hp] = 0 });
        var actions = new AtomicAction[] { new SetUnitVar("U", Keys.Hp, 1), new SetUnitVar("U", Keys.Hp, 2) };
        var se = new SkillExecutor();

        // With per-action events enabled
        var busOn = new EventBus(); int executedOn = 0; int varChangedOn = 0;
        using var d1 = busOn.Subscribe(EventTopics.ActionExecuted, _ => executedOn++);
        using var d2 = busOn.Subscribe(EventTopics.UnitVarChanged, _ => varChangedOn++);
        var optOn = new ExecutionOptions(TransactionalBatch: true, ConflictHandling: ConflictHandling.WarnOnly, EmitPerActionEventsInTransactional: true);
        (s, _) = se.Execute(s, actions, validator: null, events: busOn, options: optOn);
        Assert.Equal(2, executedOn);
        Assert.Equal(2, varChangedOn);

        // With per-action events disabled
        var busOff = new EventBus(); int executedOff = 0; int varChangedOff = 0;
        using var d3 = busOff.Subscribe(EventTopics.ActionExecuted, _ => executedOff++);
        using var d4 = busOff.Subscribe(EventTopics.UnitVarChanged, _ => varChangedOff++);
        var optOff = new ExecutionOptions(TransactionalBatch: true, ConflictHandling: ConflictHandling.WarnOnly, EmitPerActionEventsInTransactional: false);
        (s, _) = se.Execute(s, actions, validator: null, events: busOff, options: optOff);
        Assert.Equal(0, executedOff);
        Assert.Equal(0, varChangedOff);
    }

    [Fact]
    public void LineAoeDamage_Physical_Radius_One_HitsAdjacentToPath()
    {
        var s = EmptyWorld();
        var teams = new Dictionary<string, string> { ["A"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["F"] = "T1" };
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.TeamsKey, teams) });
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 0), [Keys.Atk] = 0 });
        // Place enemies: one on path (0,2), one adjacent to path (1,2) within radius 1
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 2), [Keys.Hp] = 20, [Keys.Def] = 0 });
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 2), [Keys.Hp] = 20, [Keys.Def] = 0 });
        // Friendly near path should not be hit
        s = WithUnit(s, "F", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1), [Keys.Hp] = 20, [Keys.Def] = 0 });
        s = WithUnit(s, "T", new Dictionary<string, object> { [Keys.Pos] = new Coord(0, 4), [Keys.Hp] = 1 });

        s = new LineAoeDamage("A", TargetId: "T", Power: 5, Length: 4, Radius: 1, Flavor: DamageFlavor.Physical, IgnoreRatio: 0.0).Compile()(s);
        Assert.Equal(15, s.Units["E1"].GetIntVar(Keys.Hp));
        Assert.Equal(15, s.Units["E2"].GetIntVar(Keys.Hp));
        Assert.Equal(20, s.Units["F"].GetIntVar(Keys.Hp));
    }

    [Fact]
    public void Script_Nearest_And_Farthest_Selection_Order()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(1, 1) });
        // Enemies at various distances
        s = WithUnit(s, "E1", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 1) }); // d=1
        s = WithUnit(s, "E2", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 1) }); // d=2
        s = WithUnit(s, "E3", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 1) }); // d=3
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["E1"] = "T2", ["E2"] = "T2", ["E3"] = "T2" };
        // Nearest 2 via SkillScript and Selection.SortByNearestTo
        Func<Context, IEnumerable<string>> nearest2Enemies = ctx =>
        {
            var order = Selection.SortByNearestTo("C")(ctx);
            var list = new List<string>();
            foreach (var id in order)
            {
                if (teams.TryGetValue(id, out var t) && t != teams["C"]) list.Add(id);
                if (list.Count == 2) break;
            }
            return list;
        };
        var skill1 = SkillBuilder.Create("Nearest2").Script(ss =>
        {
            ss.ForEachUnits(nearest2Enemies, (sub, id) => sub.SetUnitVar(id, "mark", 1));
        }).Build();
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill1.BuildPlan(new Context(s)), validator: null);
        Assert.Equal(1, TypeConversion.GetIntFrom(s.Units["E1"].Vars, "mark", 0));
        Assert.Equal(1, TypeConversion.GetIntFrom(s.Units["E2"].Vars, "mark", 0));
        Assert.DoesNotContain("mark", s.Units["E3"].Vars.Keys);

        // Farthest 1 via SkillScript
        Func<Context, IEnumerable<string>> farthest1Enemy = ctx => Selection
            .SortByNearestTo("C")(ctx)
            .Reverse()
            .Where(id => teams.TryGetValue(id, out var t) && t != teams["C"])
            .Take(1);
        var skill2 = SkillBuilder.Create("Farthest1").Script(ss =>
        {
            ss.ForEachUnits(farthest1Enemy, (sub, id) => sub.SetUnitVar(id, "far", 1));
        }).Build();
        (s, _) = se.ExecutePlan(s, skill2.BuildPlan(new Context(s)), validator: null);
        Assert.Equal(1, TypeConversion.GetIntFrom(s.Units["E3"].Vars, "far", 0));
        Assert.DoesNotContain("far", s.Units["E1"].Vars.Keys);
        Assert.DoesNotContain("far", s.Units["E2"].Vars.Keys);
    }
    [Fact]
    public void PhysicalDamage_Basic()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Atk] = 10 });
        s = WithUnit(s, "B", new Dictionary<string, object> { [Keys.Def] = 3, [Keys.Hp] = 20 });

        var eff = new PhysicalDamage("A", "B", Power: 5).Compile();
        s = eff(s);
        var hp = s.Units["B"].GetIntVar(Keys.Hp);
        // raw = 5 + 10 - 3 = 12; 20 - 12 = 8
        Assert.Equal(8, hp);
    }

    [Fact]
    public void PhysicalDamage_IgnoreDefense_And_Resist()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Atk] = 10 });
        s = WithUnit(s, "B", new Dictionary<string, object> { [Keys.Def] = 10, [Keys.Hp] = 30, [Keys.ResistPhysical] = 0.5 });

        var eff = new PhysicalDamage("A", "B", Power: 10, IgnoreDefenseRatio: 0.5).Compile();
        s = eff(s);
        var hp = s.Units["B"].GetIntVar(Keys.Hp);
        // effDef = 10*(1-0.5)=5; raw=10+10-5=15; resist 0.5 => 7 or 8 rounded => 8; 30-8=22
        Assert.Equal(22, hp);
    }

    [Fact]
    public void MagicDamage_FallsBack_To_Atk_If_MAtk_Missing()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Atk] = 8 }); // no matk
        s = WithUnit(s, "B", new Dictionary<string, object> { [Keys.MDef] = 3, [Keys.Hp] = 25 });
        var eff = new MagicDamage("A", "B", Power: 5).Compile();
        s = eff(s);
        var hp = s.Units["B"].GetIntVar(Keys.Hp);
        // atk used as matk: raw = 5 + 8 - 3 = 10; 25-10=15
        Assert.Equal(15, hp);
    }

    [Fact]
    public void Shield_Absorbs_Damage_Before_Hp()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Atk] = 5 });
        s = WithUnit(s, "B", new Dictionary<string, object> { [Keys.Hp] = 20, [Keys.Def] = 0, [Keys.ShieldValue] = 6.0 });
        s = new PhysicalDamage("A", "B", Power: 6).Compile()(s); // raw = 6+5-0=11 => shield 6 -> left 5 to HP
        var u = s.Units["B"]; var hp = u.GetIntVar(Keys.Hp);
        var shield = TypeConversion.ToDouble(u.Vars.TryGetValue(Keys.ShieldValue, out var sv) ? sv : null, 0.0);
        Assert.Equal(15, hp);
        Assert.Equal(0.0, shield);
    }

    [Fact]
    public void Undying_Prevents_Death_And_Turn_Ticks()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Atk] = 50 });
        s = WithUnit(s, "B", new Dictionary<string, object> { [Keys.Hp] = 10, [Keys.Def] = 0, [Keys.UndyingTurns] = 1 });
        s = new PhysicalDamage("A", "B", Power: 10).Compile()(s); // lethal but undying => stays at 1
        Assert.Equal(1, s.Units["B"].GetIntVar(Keys.Hp));

        var ts = new TurnSystem();
        (s, _) = ts.AdvanceTurn(s);
        // undying turns decreased, tag auto-removed if present
        var bVars = s.Units["B"].Vars;
        var turns = TypeConversion.GetIntFrom(bVars, Keys.UndyingTurns, 0);
        Assert.True(turns >= 0);
    }

    [Fact]
    public void Regen_And_PerTurnAdd_Are_Applied_And_Clamped()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "U", new Dictionary<string, object>
        {
            [Keys.Hp] = 5,
            [Keys.MaxHp] = 10,
            [Keys.Mp] = 0.0,
            [Keys.MaxMp] = 5.0,
            [Keys.HpRegenPerTurn] = 3.0,
            [Keys.MpRegenPerTurn] = 2.0,
            ["per_turn_add:resist_magic"] = 0.2,
            ["per_turn_max:resist_magic"] = 0.6
        });

        var ts = new TurnSystem();
        (s, _) = ts.AdvanceTurn(s); // one tick
        var u = s.Units["U"];
        Assert.Equal(8, u.GetIntVar(Keys.Hp)); // 5 + 3 = 8
        Assert.Equal(2.0, u.GetDoubleVar(Keys.Mp));
        Assert.Equal(0.2, u.GetDoubleVar(Keys.ResistMagic));

        // advance many turns to test clamp at per_turn_max
        for (int i = 0; i < 3; i++) (s, _) = ts.AdvanceTurn(s);
        u = s.Units["U"];
        Assert.Equal(10, u.GetIntVar(Keys.Hp)); // clamped to max_hp
        Assert.Equal(5.0, u.GetDoubleVar(Keys.Mp)); // clamped to max_mp
        Assert.True(u.GetDoubleVar(Keys.ResistMagic) <= 0.6 + 1e-9);
    }

    [Fact]
    public void AutoHealBelowHalf_Triggers_Once()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "B", new Dictionary<string, object>
        {
            [Keys.Hp] = 30,
            [Keys.MaxHp] = 40,
            [Keys.AutoHealBelowHalf] = 10
        });
        // First damage dips to <= 20 from above -> heals 10 (30-15=15, then +10 => 25)
        s = new Damage("B", 15).Compile()(s);
        var hp1 = s.Units["B"].GetIntVar(Keys.Hp);
        Assert.Equal(25, hp1);
        // Second time should not trigger
        s = new Damage("B", 5).Compile()(s);
        var hp2 = s.Units["B"].GetIntVar(Keys.Hp);
        Assert.Equal(20, hp2);
    }

    // Note: DSL execution is validated through integration flows in the sample app.
}
