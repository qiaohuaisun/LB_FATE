using ETBBS;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Xunit;

public class KateRoleTests
{
    private static string? FindRepoRolesDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var probe = Path.Combine(dir, "roles");
            if (Directory.Exists(probe)) return probe;
            var parent = Directory.GetParent(dir)?.FullName;
            if (string.IsNullOrEmpty(parent)) break;
            dir = parent;
        }
        return null;
    }

    private static (WorldState s, Context ctx) MakeWorldWithCasterTarget()
    {
        var s = WorldState.CreateEmpty(8, 8);
        // A = caster, B = target
        s = WorldStateOps.WithUnit(s, "A", _ => new UnitState(
            ImmutableDictionary<string, object>.Empty
                .Add(Keys.Pos, new Coord(1, 1))
                .Add(Keys.Mp, 10.0),
            ImmutableHashSet<string>.Empty));
        s = WorldStateOps.WithUnit(s, "B", _ => new UnitState(
            ImmutableDictionary<string, object>.Empty
                .Add(Keys.Pos, new Coord(3, 2))
                .Add(Keys.Hp, 25)
                .Add(Keys.Mp, 4.5),
            ImmutableHashSet<string>.Empty));
        var teams = new Dictionary<string, string> { ["A"] = "T1", ["B"] = "T2" };
        s = WorldStateOps.WithGlobal(s, g => g with
        {
            Vars = g.Vars
            .SetItem(DslRuntime.CasterKey, "A")
            .SetItem(DslRuntime.TargetKey, "B")
            .SetItem(DslRuntime.TeamsKey, teams)
        });
        return (s, new Context(s));
    }

    [Fact]
    public void Kate_Loads_And_Skills_Metadata_Are_Correct()
    {
        var rolesDir = FindRepoRolesDir();
        if (rolesDir is null) return; // allow running outside repo tree
        var path = Path.Combine(rolesDir, "kate.lbr");
        var role = LbrLoader.LoadFromFile(path);
        Assert.Equal("caster_kate", role.Id);
        Assert.Contains("caster", role.Tags);
        Assert.True(role.Skills.Length >= 2);

        var s1 = role.Skills.First(s => s.Name.Contains("鉴识眼"));
        Assert.Equal(3, s1.Compiled.Metadata.MpCost);
        Assert.Equal(2, s1.Compiled.Metadata.Range);
        Assert.True(s1.Compiled.Extras.TryGetValue("targeting", out var tgt) && (tgt as string) == "any");
        Assert.True(s1.Compiled.Extras.TryGetValue("cooldown", out var cd1) && (int)cd1! == 0);

        var s2 = role.Skills.First(s => s.Name.Contains("号召"));
        Assert.Equal(5, s2.Compiled.Metadata.MpCost);
        Assert.Equal(2, s2.Compiled.Metadata.Range);
        Assert.True(s2.Compiled.Extras.TryGetValue("targeting", out var tgt2) && (tgt2 as string) == "any");
        Assert.True(s2.Compiled.Extras.TryGetValue("cooldown", out var cd2) && (int)cd2! == 2);
    }

    [Fact]
    public void Kate_Skill1_Inspect_Sets_Global_Vars()
    {
        var rolesDir = FindRepoRolesDir();
        if (rolesDir is null) return;
        var path = Path.Combine(rolesDir, "kate.lbr");
        var role = LbrLoader.LoadFromFile(path);
        var s1 = role.Skills.First(s => s.Name.Contains("鉴识眼"));

        var (s, _) = MakeWorldWithCasterTarget();
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, s1.Compiled.BuildPlan(new Context(s)), validator: null);
        Assert.True(s.Global.Vars.TryGetValue("inspect_hp", out var hpObj));
        Assert.True(s.Global.Vars.TryGetValue("inspect_mp", out var mpObj));
        Assert.True(s.Global.Vars.TryGetValue("inspect_pos", out var posObj));
        Assert.Equal(25, Convert.ToInt32(hpObj));
        Assert.Equal(4.5, Convert.ToDouble(mpObj), 6);
        Assert.IsType<Coord>(posObj);
        Assert.Equal(new Coord(3, 2), (Coord)posObj);
    }

    [Fact]
    public void Kate_Skill2_ForceAct_Adds_Global_Tag_And_Requires_MP()
    {
        var rolesDir = FindRepoRolesDir();
        if (rolesDir is null) return;
        var path = Path.Combine(rolesDir, "kate.lbr");
        var role = LbrLoader.LoadFromFile(path);
        var s2 = role.Skills.First(s => s.Name.Contains("号召"));

        // Enough MP: tag should be added
        var (sEnough, _) = MakeWorldWithCasterTarget();
        var se = new SkillExecutor();
        (sEnough, _) = se.ExecutePlan(sEnough, s2.Compiled.BuildPlan(new Context(sEnough)), validator: null);
        Assert.Contains("force_act_now", sEnough.Global.Tags);

        // Validator denies when MP insufficient
        var (sLow, _) = MakeWorldWithCasterTarget();
        sLow = WorldStateOps.WithUnit(sLow, "A", u => u with { Vars = u.Vars.SetItem(Keys.Mp, 1.0) });
        var cfg = new ActionValidationConfig(
            CasterId: "A",
            TargetUnitId: "B",
            TeamOfUnit: new Dictionary<string, string> { ["A"] = "T1", ["B"] = "T2" },
            Targeting: TargetingMode.Any,
            CurrentTurn: 1,
            CurrentDay: 1,
            CurrentPhase: 1
        );
        var validator = ActionValidators.ForSkillWithExtras(s2.Compiled, cfg, cooldownStore: null);
        var plan = s2.Compiled.BuildPlan(new Context(sLow));
        var firstBatch = plan.Count > 0 ? plan[0] : Array.Empty<AtomicAction>();
        var ok = validator(new Context(sLow), firstBatch, out var reason);
        Assert.False(ok);
        Assert.NotNull(reason);
        Assert.Contains("Not enough MP", reason!);
    }
}
