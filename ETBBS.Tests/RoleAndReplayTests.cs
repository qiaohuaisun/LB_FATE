using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using ETBBS;
using Xunit;

public class RoleAndReplayTests
{
    private static WorldState EmptyWorld(int w = 10, int h = 10)
        => WorldState.CreateEmpty(w, h);

    [Fact]
    public void RoleParser_Parses_Vars_Tags_Skills_And_CompiledExec()
    {
        var text = """
role "Test Role" id "test_role" {
  description "Hello\nWorld";
  vars { "hp" = 30; "mp" = 5.5; "class" = "Saber" }
  tags { "tag1", "tag2" }
  skills {
    skill "S1" {
      // set a global flag to verify execution
      set global var "s1" = 1
    }
    skill "S2" { /* empty on purpose */ }
  }
}
""";

        var role = LbrLoader.Load(text);
        Assert.Equal("test_role", role.Id);
        Assert.Equal("Test Role", role.Name);
        Assert.Contains("Hello", role.Description);
        Assert.Equal(30, (int)Convert.ToInt32(role.Vars[Keys.Hp]));
        Assert.Equal(5.5, Convert.ToDouble(role.Vars[Keys.Mp]), 6);
        Assert.Contains("tag1", role.Tags);
        Assert.Contains("tag2", role.Tags);
        Assert.Equal(2, role.Skills.Length);
        Assert.Equal("S1", role.Skills[0].Name);
        Assert.NotNull(role.Skills[0].Compiled);

        // Execute the compiled S1 to ensure it runs and sets global var
        var s = EmptyWorld();
        s = WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(DslRuntime.CasterKey, "U") });
        s = WorldStateOps.WithUnit(s, "U", _ => new UnitState(ImmutableDictionary<string, object>.Empty.Add(Keys.Pos, new Coord(0,0)), ImmutableHashSet<string>.Empty));
        var se = new SkillExecutor();
        (s, _) = se.ExecutePlan(s, role.Skills[0].Compiled.BuildPlan(new Context(s)), validator: null);
        Assert.Equal(1, (int)Convert.ToInt32(s.Global.Vars["s1"]));
    }

    [Fact]
    public void RoleRegistry_LoadDirectory_Recursive_Works()
    {
        var root = Path.Combine(Path.GetTempPath(), "etbbs-tests-" + Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(root, "sub");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(sub);
        try
        {
            var r1 = Path.Combine(root, "r1.lbr");
            File.WriteAllText(r1, "role \"R1\" id \"r1\" { skills { skill \"A\" { } } }");
            var r2 = Path.Combine(sub, "r2.lbr");
            File.WriteAllText(r2, "role \"R2\" id \"r2\" { skills { skill \"B\" { } } }");

            var reg1 = new RoleRegistry().LoadDirectory(root, recursive: false);
            Assert.True(reg1.TryGet("r1", out var _));
            Assert.False(reg1.TryGet("r2", out var _));

            var reg2 = new RoleRegistry().LoadDirectory(root, recursive: true);
            Assert.True(reg2.TryGet("r1", out var _));
            Assert.True(reg2.TryGet("r2", out var _));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ReplaySystem_Records_FinalState_And_Logs()
    {
        var s0 = EmptyWorld();
        s0 = WorldStateOps.WithUnit(s0, "U", _ => new UnitState(
            ImmutableDictionary<string, object>.Empty.Add(Keys.Hp, 0),
            ImmutableHashSet<string>.Empty));

        var steps = new List<AtomicAction[]>
        {
            new [] { new SetUnitVar("U", Keys.Hp, 10) },
            new [] { new Damage("U", 3) }
        };

        var replay = new ReplaySystem().Record(s0, steps);
        // Initial remains unchanged
        Assert.Equal(0, (int)Convert.ToInt32(replay.Initial.Units["U"].Vars[Keys.Hp]));
        // Final has applied changes
        Assert.Equal(7, (int)Convert.ToInt32(replay.Final.Units["U"].Vars[Keys.Hp]));
        // Logs captured per step
        Assert.Equal(2, replay.Logs.Count);
        Assert.True(replay.Logs[0].Count > 0);
        Assert.True(replay.Logs[1].Count > 0);
    }
}
