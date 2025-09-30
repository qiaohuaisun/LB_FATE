using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ETBBS;
using Xunit;

public class BossModeTests
{
    private static string? FindRepoRolesDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var probe = Path.Combine(dir, "roles");
            if (Directory.Exists(probe)) return probe;
            var parent = Directory.GetParent(dir)?.FullName;
            if (string.IsNullOrEmpty(parent)) break;
            dir = parent;
        }
        return null;
    }

    private static object Invoke(object target, string method, params object?[] args)
    {
        var mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return mi.Invoke(target, args)!;
    }

    private static T GetField<T>(object target, string name)
    {
        var fi = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return (T)fi.GetValue(target)!;
    }

    private static void SetField(object target, string name, object value)
    {
        var fi = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        fi.SetValue(target, value);
    }

    [Fact]
    public void BossMode_AiActs_And_DamagesNearbyEnemy()
    {
        Environment.SetEnvironmentVariable("LB_FATE_MODE", "boss");
        var rolesDir = FindRepoRolesDir();
        Assert.NotNull(rolesDir);

        var asm = Assembly.Load("LB_FATE");
        var gameType = asm.GetType("LB_FATE.Game")!;
        var game = Activator.CreateInstance(gameType, new object?[] { rolesDir, 2, null, 7, 5 })!;
        Invoke(game, "InitWorld");

        // Place P1 near boss to ensure in range
        var state = GetField<WorldState>(game, "state");
        var bossId = GetField<string>(game, "bossId");
        var bossPos = state.Units.TryGetValue(bossId, out var bu) && bu.Vars.TryGetValue(Keys.Pos, out var bp) ? (Coord)bp : new Coord(3, 2);
        state = WorldStateOps.WithUnit(state, "P1", u => u with { Vars = u.Vars.SetItem(Keys.Pos, new Coord(Math.Max(0, bossPos.X - 1), bossPos.Y)).SetItem(Keys.Hp, 40).SetItem(Keys.MaxHp, 40) });
        SetField(game, "state", state);

        // Record HP and distance before
        state = GetField<WorldState>(game, "state");
        var hpBefore = (int)Convert.ToInt32(state.Units["P1"].Vars[Keys.Hp]);
        int distBefore = Math.Abs(bossPos.X - ((Coord)state.Units["P1"].Vars[Keys.Pos]).X) + Math.Abs(bossPos.Y - ((Coord)state.Units["P1"].Vars[Keys.Pos]).Y);

        // Boss AI turn: allow two phases to account for telegraphs
        Invoke(game, "Turn", bossId, 1, 1);
        Invoke(game, "Turn", bossId, 2, 1);

        // After AI action, P1 should have taken damage or the board advanced
        state = GetField<WorldState>(game, "state");
        var hpAfter = (int)Convert.ToInt32(state.Units["P1"].Vars[Keys.Hp]);
        var newBossPos = (Coord)state.Units["BOSS"].Vars[Keys.Pos];
        int distAfter = Math.Abs(newBossPos.X - ((Coord)state.Units["P1"].Vars[Keys.Pos]).X) + Math.Abs(newBossPos.Y - ((Coord)state.Units["P1"].Vars[Keys.Pos]).Y);
        Assert.True(hpAfter < hpBefore || distAfter < distBefore);
    }
}
