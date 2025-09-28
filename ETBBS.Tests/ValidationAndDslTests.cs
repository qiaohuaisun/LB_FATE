using ETBBS;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;

public class ValidationAndDslTests
{
    private static WorldState EmptyWorld(int w = 10, int h = 10)
        => WorldState.CreateEmpty(w, h);

    private static WorldState WithUnit(WorldState s, string id, IDictionary<string, object> vars)
    {
        var imVars = ImmutableDictionary.CreateRange(vars);
        return WorldStateOps.WithUnit(s, id, _ => new UnitState(imVars, ImmutableHashSet<string>.Empty));
    }

    [Fact]
    public void Validator_MpCost_And_ConsumeMp()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Mp] = 1.0, [Keys.Pos] = new Coord(1, 1) });
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "C") });

        var script = "cost mp 2; targeting self; set global var \"ok\" = 1; consume mp = 2";
        var skill = TextDsl.FromTextUsingGlobals("MpSkill", script);

        var cfg = new ActionValidationConfig(CasterId: "C", CurrentTurn: 1);
        var validator = ActionValidators.ForSkillWithExtras(skill, cfg, cooldownStore: null);
        var se = new SkillExecutor();

        // Not enough MP -> blocked (validator)
        {
            var ctx0 = new Context(s);
            var allowed0 = validator(ctx0, skill.BuildActions(ctx0), out var reason0);
            Assert.False(allowed0);
        }

        // Give enough MP -> allowed; consume should reduce MP
        s = WorldStateOps.WithUnit(s, "C", u => u with { Vars = u.Vars.SetItem(Keys.Mp, 2.0) });
        // Enough MP -> allowed
        {
            var ctx1 = new Context(s);
            var allowed1 = validator(ctx1, skill.BuildActions(ctx1), out var reason1);
            Assert.True(allowed1, reason1);
        }
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: validator);
        var mpObj = s.Units["C"].Vars[Keys.Mp];
        var mp = mpObj is double d ? d : (mpObj is int i ? (double)i : Convert.ToDouble(mpObj));
        Assert.Equal(0.0, mp, 6);
    }

    [Fact]
    public void Validator_Targeting_SelfOnly()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 2) });
        s = WithUnit(s, "X", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 2) });
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "C") });

        var script = "targeting self; range 0; set global var \"ok\" = 1";
        var skill = TextDsl.FromTextUsingGlobals("SelfOnly", script);
        var se = new SkillExecutor();

        // Correct: target self
        var cfgOk = new ActionValidationConfig(CasterId: "C", TargetUnitId: "C", TeamOfUnit: new Dictionary<string, string> { { "C", "T" } }, CurrentTurn: 0);
        var valOk = ActionValidators.ForSkillWithExtras(skill, cfgOk, null);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valOk);
        Assert.Equal(1, (int)Convert.ToInt32(s.Global.Vars["ok"]));

        // Wrong: target other unit -> blocked
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.Remove("ok") });
        var cfgBad = new ActionValidationConfig(CasterId: "C", TargetUnitId: "X", TeamOfUnit: new Dictionary<string, string> { { "C", "T" }, { "X", "T2" } }, CurrentTurn: 0);
        var valBad = ActionValidators.ForSkillWithExtras(skill, cfgBad, null);
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valBad);
        Assert.False(s.Global.Vars.ContainsKey("ok"));
    }

    [Fact]
    public void Validator_Targeting_Allies_And_Enemies()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 2) });
        s = WithUnit(s, "A", new Dictionary<string, object> { [Keys.Pos] = new Coord(3, 2) });
        s = WithUnit(s, "E", new Dictionary<string, object> { [Keys.Pos] = new Coord(4, 2) });
        var teams = new Dictionary<string, string> { ["C"] = "T1", ["A"] = "T1", ["E"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "C").SetItem(DslRuntime.TargetKey, "A").SetItem(DslRuntime.TeamsKey, teams) });

        var scriptAllies = "targeting allies; range 5; set global var \"ok\" = 1";
        var skillAllies = TextDsl.FromTextUsingGlobals("AlliesOnly", scriptAllies);
        var se = new SkillExecutor();

        // Allies -> pass
        var cfgAllies = new ActionValidationConfig(CasterId: "C", TargetUnitId: "A", TeamOfUnit: teams, CurrentTurn: 0);
        var valAllies = ActionValidators.ForSkillWithExtras(skillAllies, cfgAllies, null);
        {
            var ctx = new Context(s);
            var allowed = valAllies(ctx, skillAllies.BuildActions(ctx), out var reason);
            Assert.True(allowed, reason);
        }
        (s, _) = se.ExecutePlan(s, skillAllies.BuildPlan(new Context(s)), validator: valAllies);

        // Enemies -> should block with allies targeting
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.TargetKey, "E").Remove("ok") });
        var cfgBad = cfgAllies with { TargetUnitId = "E" };
        var valBad = ActionValidators.ForSkillWithExtras(skillAllies, cfgBad, null);
        {
            var ctx = new Context(s);
            var allowed = valBad(ctx, skillAllies.BuildActions(ctx), out var reason);
            Assert.False(allowed);
        }

        // Enemies skill -> pass for enemy
        var scriptEnemies = "targeting enemies; range 5; set global var \"ok\" = 1";
        var skillEnemies = TextDsl.FromTextUsingGlobals("EnemiesOnly", scriptEnemies);
        var cfgEnemies = new ActionValidationConfig(CasterId: "C", TargetUnitId: "E", TeamOfUnit: teams, CurrentTurn: 0);
        var valEnemies = ActionValidators.ForSkillWithExtras(skillEnemies, cfgEnemies, null);
        {
            var ctx = new Context(s);
            var allowed = valEnemies(ctx, skillEnemies.BuildActions(ctx), out var reason);
            Assert.True(allowed, reason);
        }
        (s, _) = se.ExecutePlan(s, skillEnemies.BuildPlan(new Context(s)), validator: valEnemies);
    }

    [Fact]
    public void Validator_MinRange_With_TileTarget()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Pos] = new Coord(2, 2) });
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "C") });

        var script = "range 3; min_range 2; targeting tile; set global var \"ok\" = 1";
        var skill = TextDsl.FromTextUsingGlobals("MinRangeTile", script);
        var se = new SkillExecutor();

        // Too close: distance 1 -> blocked
        var cfgClose = new ActionValidationConfig(CasterId: "C", TargetPos: new Coord(3, 2));
        var valClose = ActionValidators.ForSkillWithExtras(skill, cfgClose, null);
        {
            var ctx = new Context(s);
            var allowed = valClose(ctx, skill.BuildActions(ctx), out var reason);
            Assert.False(allowed);
        }

        // Within allowed: distance 2 -> pass
        var cfgOk = cfgClose with { TargetPos = new Coord(4, 2) };
        var valOk = ActionValidators.ForSkillWithExtras(skill, cfgOk, null);
        {
            var ctx = new Context(s);
            var allowed = valOk(ctx, skill.BuildActions(ctx), out var reason);
            Assert.True(allowed, reason);
        }
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: valOk);
    }

    [Fact]
    public void Dsl_Arithmetic_VarRefs_Add_Sub_IntDouble()
    {
        var s = EmptyWorld();
        s = WithUnit(s, "C", new Dictionary<string, object> { [Keys.Atk] = 5, [Keys.Pos] = new Coord(0, 0) });
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "C") });

        var script = "set global var \"sum\" = var \"atk\" of caster + 2; set global var \"dif\" = var \"atk\" of caster - 1; set global var \"mix\" = var \"atk\" of caster + 1.5";
        var skill = TextDsl.FromTextUsingGlobals("Exprs", script);
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, skill.BuildPlan(new Context(s)), validator: null);

        Assert.Equal(7, (int)Convert.ToInt32(s.Global.Vars["sum"]));
        Assert.Equal(4, (int)Convert.ToInt32(s.Global.Vars["dif"]));
        Assert.Equal(6.5, Convert.ToDouble(s.Global.Vars["mix"]), 6);
    }
}
