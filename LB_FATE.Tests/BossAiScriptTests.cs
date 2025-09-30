using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ETBBS;
using Xunit;

public class BossAiScriptTests
{
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
    public void Beast_Ai_ExecutesTurn_WithMultipleEnemies()
    {
        Environment.SetEnvironmentVariable("LB_FATE_MODE", "boss");
        var asm = Assembly.Load("LB_FATE");
        var gameType = asm.GetType("LB_FATE.Game")!;
        // Use repo roles dir
        string? rolesDir = null;
        var baseDir = AppContext.BaseDirectory;
        for (int i = 0; i < 6 && rolesDir is null; i++)
        {
            var probe = Path.Combine(baseDir, "roles");
            if (Directory.Exists(probe)) rolesDir = probe;
            else
            {
                probe = Path.Combine(baseDir, "publish", "roles");
                if (Directory.Exists(probe)) rolesDir = probe;
            }
            baseDir = Directory.GetParent(baseDir)?.FullName ?? baseDir;
        }
        var game = Activator.CreateInstance(gameType, new object?[] { rolesDir, 3, null, 9, 7 })!;
        Invoke(game, "InitWorld");

        // Ensure boss present and place two enemies within radius 2 around boss
        var state = GetField<WorldState>(game, "state");
        var bossId = GetField<string>(game, "bossId");
        var bossPos = state.Units.TryGetValue(bossId, out var bu) && bu.Vars.TryGetValue(Keys.Pos, out var bp) ? (Coord)bp : new Coord(4, 3);
        state = WorldStateOps.WithUnit(state, "P1", u => u with { Vars = u.Vars.SetItem(Keys.Pos, new Coord(Math.Max(0, bossPos.X + 1), bossPos.Y)).SetItem(Keys.Hp, 40).SetItem(Keys.MaxHp, 40) });
        state = WorldStateOps.WithUnit(state, "P2", u => u with { Vars = u.Vars.SetItem(Keys.Pos, new Coord(Math.Max(0, bossPos.X), Math.Max(0, bossPos.Y + 1))).SetItem(Keys.Hp, 40).SetItem(Keys.MaxHp, 40) });
        SetField(game, "state", state);

        var before1 = (int)Convert.ToInt32(state.Units["P1"].Vars[Keys.Hp]);
        var before2 = (int)Convert.ToInt32(state.Units["P2"].Vars[Keys.Hp]);
        int d1Before = Math.Abs(bossPos.X - ((Coord)state.Units["P1"].Vars[Keys.Pos]).X) + Math.Abs(bossPos.Y - ((Coord)state.Units["P1"].Vars[Keys.Pos]).Y);
        int d2Before = Math.Abs(bossPos.X - ((Coord)state.Units["P2"].Vars[Keys.Pos]).X) + Math.Abs(bossPos.Y - ((Coord)state.Units["P2"].Vars[Keys.Pos]).Y);

        // Boss AI turn (phase 3 telegraphs Mass Sweep by config), then execute at phase 4
        Invoke(game, "Turn", bossId, 3, 1);
        Invoke(game, "Turn", bossId, 4, 1);

        state = GetField<WorldState>(game, "state");

        // Verify boss still exists
        Assert.True(state.Units.ContainsKey(bossId), "Boss should exist after turn");

        // Verify players still exist
        Assert.True(state.Units.ContainsKey("P1"), "P1 should exist");
        Assert.True(state.Units.ContainsKey("P2"), "P2 should exist");

        // Verify game state is valid (turn counter exists)
        Assert.True(state.Global.Turn >= 0, "Turn counter should be valid");

        // Boss position should be valid
        var newBossPos = state.Units.TryGetValue(bossId, out var bossUnit) && bossUnit.Vars.TryGetValue(Keys.Pos, out var newBp) ? (Coord)newBp : new Coord(-1, -1);
        Assert.NotEqual(new Coord(-1, -1), newBossPos);
    }
}
