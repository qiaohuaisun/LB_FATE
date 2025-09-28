using System;
using System.Collections.Generic;
using System.Linq;
using ETBBS;

namespace LB_FATE;

partial class Game
{
    private void Turn(string pid, int phase, int day)
    {
        Context ctx;
        // speed/range/rooted/silenced will be recomputed dynamically each loop
        IPlayerEndpoint? ep = endpoints.TryGetValue(pid, out var ept) ? ept : null;

        while (true)
        {
            // refresh context/dynamic stats each loop to reflect latest state after previous actions
            ctx = new Context(state);
            var speed = GetInt(pid, Keys.Speed, 3);
            var range = GetInt(pid, Keys.Range, 1);
            bool isRooted = state.Units[pid].Tags.Contains(Tags.Rooted);
            bool isSilenced = state.Units[pid].Tags.Contains(Tags.Silenced);
            if (ep is not null)
            {
                ep.SendLine("PROMPT");
                var line = ep.ReadLine() ?? string.Empty;
                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var cmd = parts[0].ToLowerInvariant();
                bool restricted = (phase is >= 2 and <= 4);
                if (restricted && cmd is not ("move" or "m" or "pass" or "p" or "help" or "h"))
                { WriteLineTo(pid, "Phases 2-4: only move/pass allowed."); continue; }

                if (cmd is "help" or "h")
                {
                    WriteLineTo(pid, "move x y | m x y : move to a reachable tile (clear path ≤ speed, cost: 0.5 MP)");
                    WriteLineTo(pid, "attack P# | a P# : attack target (cost: 0.5 MP; LBR Basic Attack if exists)");
                    WriteLineTo(pid, "skills | s       : list available skills");
                    WriteLineTo(pid, "info | i         : show role description");
                    WriteLineTo(pid, "use n P# | u n P#: cast skill #n (target optional by targeting)");
                    WriteLineTo(pid, "hint move|hm     : highlight reachable tiles");
                    WriteLineTo(pid, "hint attack|ha   : highlight enemies in range");
                    WriteLineTo(pid, "pass | p         : end your turn");
                    WriteLineTo(pid, "Costs           : Move 0.5 MP; Attack 0.5 MP");
                    WriteLineTo(pid, "Phases          : 1 & 5 all commands; 2-4 move/pass only");
                    continue;
                }
                if (cmd is "info" or "i")
                {
                    if (roleOf.ContainsKey(pid))
                    {
                        var role = roleOf[pid];
                        WriteLineTo(pid, $"{role.Name} ({role.Id})");
                        var desc = role.Description;
                        if (string.IsNullOrWhiteSpace(desc)) WriteLineTo(pid, "  No description available.");
                        else foreach (var ln in desc.Split('\n')) WriteLineTo(pid, "  " + ln.TrimEnd());
                    }
                    else
                    {
                        WriteLineTo(pid, "No LBR role for this unit.");
                    }
                    continue;
                }
                if (cmd == "quit") { Environment.Exit(0); }
                if (cmd is "pass" or "p") break;
                if (cmd is "hint" or "hm" or "ha")
                {
                    if (cmd == "hm" || (cmd == "hint" && parts.Length >= 2 && parts[1] == "move"))
                    {
                        if (isRooted)
                        {
                            // rooted/frozen: cannot move; show no reachable tiles
                            highlightCells = new HashSet<Coord>();
                            highlightChar = 'o';
                            WriteLineTo(pid, "You are rooted/frozen and cannot move.");
                            if (ep is not null) { SendBoardTo(pid, day, phase); } else { ShowBoard(day, phase); }
                            continue;
                        }
                        highlightCells = ReachableCells(pid, speed);
                        highlightChar = 'o';
                        if (ep is not null) { SendBoardTo(pid, day, phase); } else { ShowBoard(day, phase); }
                        continue;
                    }
                    if (cmd == "ha" || (cmd == "hint" && parts.Length >= 2 && parts[1] == "attack"))
                    { highlightCells = EnemiesInRange(pid, range); highlightChar = 'x'; if (ep is not null) { SendBoardTo(pid, day, phase); } else { ShowBoard(day, phase); } continue; }
                    WriteLineTo(pid, "Usage: hint move | hint attack"); continue;
                }
                if (cmd is "move" or "m")
                {
                    if (isRooted) { WriteLineTo(pid, "You are rooted/frozen and cannot move."); continue; }
                    if (parts.Length != 3 || !int.TryParse(parts[1], out var x) || !int.TryParse(parts[2], out var y))
                    { WriteLineTo(pid, "Usage: move x y"); continue; }
                    var dest = new Coord(x, y);
                    var cur = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
                    if (dest.X < 0 || dest.X >= width || dest.Y < 0 || dest.Y >= height) { WriteLineTo(pid, "Out of bounds."); continue; }
                    // Path must be reachable within speed without passing through occupied tiles
                    var reachable = ReachableCells(pid, speed);
                    if (!reachable.Contains(dest)) { WriteLineTo(pid, "Destination not reachable (blocked path or too far)."); continue; }
                    // MP cost for moving: fixed 0.5 per move
                    var mpObj0 = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
                    double mp0 = mpObj0 is double dd0 ? dd0 : (mpObj0 is int ii0 ? ii0 : 0);
                    double moveCost = 0.5;
                    if (mp0 < moveCost) { WriteLineTo(pid, $"Not enough MP to move ({moveCost:0.##} required)."); continue; }
                    // Validator to re-assert path reachability during execution
                    ActionValidator pathValidator = (Context _, AtomicAction[] __, out string? reason) =>
                    {
                        if (!reachable.Contains(dest)) { reason = "Destination not reachable"; return false; }
                        reason = null; return true;
                    };
                    var moveValidator = pathValidator;
                    var se = new SkillExecutor();
                    var actions = new List<AtomicAction> { new Move(pid, dest), new ModifyUnitVar(pid, Keys.Mp, v => (v is double d ? d : Convert.ToDouble(v)) - moveCost) };
                    (state, var log) = se.Execute(state, actions.ToArray(), validator: moveValidator);
                    AppendLog(log.Messages);
                    BroadcastBoard(day, phase);
                    highlightCells = null;
                    continue;
                }
                if (cmd is "skills" or "s")
                {
                    if (roleOf.ContainsKey(pid))
                    {
                        var role = roleOf[pid];
                        for (int i = 0; i < role.Skills.Length; i++)
                        {
                            var s = role.Skills[i];
                            var cost = s.Compiled.Metadata.MpCost;
                            var rangeMeta = s.Compiled.Metadata.Range;
                            object? cd = s.Compiled.Extras.ContainsKey("cooldown") ? s.Compiled.Extras["cooldown"] : null;
                            object? tgt = s.Compiled.Extras.ContainsKey("targeting") ? s.Compiled.Extras["targeting"] : "any";
                            object? sealedUntil = s.Compiled.Extras.ContainsKey("sealed_until") ? s.Compiled.Extras["sealed_until"] : null;
                            int cdLeft = 0;
                            if (cd is int cdi)
                            {
                                var last = cooldowns.GetLastUseTurn(pid, s.Name) ?? int.MinValue;
                                cdLeft = Math.Max(0, last + cdi - state.Global.Turn);
                            }
                            string sealStr = "";
                            if (sealedUntil is int sut && state.Global.Turn < sut)
                            {
                                sealStr = $", seal:{sut - state.Global.Turn} left";
                            }
                            WriteLineTo(pid, $"  [{i}] {s.Name} (mp:{cost}, range:{rangeMeta}, cd:{cd ?? 0} ({cdLeft} left), tgt:{tgt}{sealStr})");
                        }
                    }
                    else
                    {
                        WriteLineTo(pid, "No LBR role loaded; only 'attack' available.");
                    }
                    continue;
                }
                if (cmd is "use" or "u")
                {
                    if (isSilenced) { WriteLineTo(pid, "You are silenced and cannot use skills."); continue; }
                    if (!roleOf.ContainsKey(pid)) { WriteLineTo(pid, "No LBR role for this unit."); continue; }
                    if (parts.Length < 2 || !int.TryParse(parts[1], out var idx)) { WriteLineTo(pid, "Usage: use <n> [P#]"); continue; }
                    var role = roleOf[pid];
                    if (idx < 0 || idx >= role.Skills.Length) { WriteLineTo(pid, "Out of range."); continue; }
                    string? tid = parts.Length >= 3 ? parts[2].ToUpperInvariant() : null;
                    if (tid != null && (!state.Units.ContainsKey(tid) || GetInt(tid, Keys.Hp, 0) <= 0))
                    { WriteLineTo(pid, "Invalid target."); continue; }
                    var skill = role.Skills[idx];
                    state = WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars
                        .SetItem(DslRuntime.CasterKey, pid)
                        .SetItem(DslRuntime.TargetKey, tid ?? "")
                        .SetItem(DslRuntime.RngKey, rng)
                        .SetItem(DslRuntime.TeamsKey, teamOf)
                    });
                    var cfg = new ActionValidationConfig(
                        CasterId: pid,
                        TargetUnitId: tid,
                        TeamOfUnit: teamOf,
                        Targeting: TargetingMode.Any,
                        CurrentTurn: state.Global.Turn
                    );
                    var validator = ActionValidators.ForSkillWithExtras(skill.Compiled, cfg, cooldowns);
                    var se = new SkillExecutor();
                    (state, var log) = se.ExecutePlan(state, skill.Compiled.BuildPlan(new Context(state)), validator);
                    AppendLog(log.Messages);
                    BroadcastBoard(day, phase);
                    cooldowns.SetLastUseTurn(pid, skill.Name, state.Global.Turn);
                    highlightCells = null;
                    continue;
                }
                if (cmd is "attack" or "a")
                {
                    if (parts.Length != 2) { WriteLineTo(pid, "Usage: attack P#"); continue; }
                    var tid = parts[1].ToUpperInvariant();
                    if (!state.Units.ContainsKey(tid) || GetInt(tid, Keys.Hp, 0) <= 0)
                    { WriteLineTo(pid, "Invalid target."); continue; }
                    var myPos = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
                    var tgPos = ctx.GetUnitVar<Coord>(tid, Keys.Pos, default);
                    var d = Math.Abs(myPos.X - tgPos.X) + Math.Abs(myPos.Y - tgPos.Y);
                    var role = roleOf.ContainsKey(pid) ? roleOf[pid] : null;
                    var basic = role?.Skills.FirstOrDefault(s => s.Name == "Basic Attack");
                    // extra strikes if within configured range (generic support)
                    int extraStrikes = 1;
                    int twinRange = 0;
                    if (state.Units[pid].Vars.TryGetValue(Keys.ExtraStrikesRange, out var xr) && xr is int rr && rr > 0) twinRange = rr;
                    if (state.Units[pid].Vars.TryGetValue(Keys.ExtraStrikesCount, out var xc) && xc is int cc && cc > 1) extraStrikes = Math.Max(1, cc);
                    if (basic is not null)
                    {
                        state = WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars
                            .SetItem(DslRuntime.CasterKey, pid)
                            .SetItem(DslRuntime.TargetKey, tid)
                            .SetItem(DslRuntime.RngKey, rng)
                            .SetItem(DslRuntime.TeamsKey, teamOf)
                        });
                        var cfg2 = new ActionValidationConfig(
                            CasterId: pid,
                            TargetUnitId: tid,
                            TeamOfUnit: teamOf,
                            Targeting: TargetingMode.EnemiesOnly,
                            CurrentTurn: state.Global.Turn
                        );
                        var validator2 = ActionValidators.ForSkillWithExtras(basic.Compiled, cfg2, cooldowns);
                        var se2 = new SkillExecutor();
                        // 普攻统一消耗 0.5 MP
                        var mpObjB = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
                        double mpB = mpObjB is double ddb ? ddb : (mpObjB is int ib ? ib : 0);
                        double basicCost = 0.5;
                        if (mpB < basicCost) { WriteLineTo(pid, "Not enough MP."); continue; }
                        int repeats = (d <= twinRange && extraStrikes > 1) ? extraStrikes : 1;
                        for (int i = 0; i < repeats; i++)
                        {
                            (state, var log) = se2.ExecutePlan(state, basic.Compiled.BuildPlan(new Context(state)), validator2);
                            AppendLog(log.Messages);
                        }
                        (state, var log2) = se2.Execute(state, new AtomicAction[] { new ModifyUnitVar(pid, Keys.Mp, v => (v is double d0 ? d0 : Convert.ToDouble(v)) - basicCost) });
                        AppendLog(log2.Messages);
                        BroadcastBoard(day, phase);
                        cooldowns.SetLastUseTurn(pid, basic.Name, state.Global.Turn);
                    }
                    else
                    {
                        if (d > range) { WriteLineTo(pid, $"Target out of range ({range})."); continue; }
                        var actions = new List<AtomicAction>();
                        // 普攻统一消耗：所有阶职 0.5 MP
                        var mpObj = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
                        double mp = mpObj is double dd ? dd : (mpObj is int i2 ? i2 : 0);
                        double cost = 0.5;
                        if (mp < cost) { WriteLineTo(pid, "Not enough MP."); continue; }
                        actions.Add(new ModifyUnitVar(pid, Keys.Mp, v => (v is double d0 ? d0 : Convert.ToDouble(v)) - cost));
                        var power = 5;
                        int repeats2 = (d <= twinRange && extraStrikes > 1) ? extraStrikes : 1;
                        for (int i = 0; i < repeats2; i++) actions.Add(new PhysicalDamage(pid, tid, power, 0.0));
                        var se3 = new SkillExecutor();
                        (state, var log) = se3.Execute(state, actions.ToArray());
                        AppendLog(log.Messages);
                        BroadcastBoard(day, phase);
                    }
                    highlightCells = null;
                    continue;
                }
                WriteLineTo(pid, "Unknown command. Type 'help'.");
            }
            else
            {
                // local console mode could be implemented similarly; for brevity, assume endpoints used
                break;
            }
        }
    }

    private int GetInt(string id, string key, int def = 0)
    {
        var u = state.Units[id];
        if (u.Vars.TryGetValue(key, out var v))
        {
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is double d) return (int)Math.Round(d);
        }
        return def;
    }

    private HashSet<Coord> ReachableCells(string pid, int speed)
    {
        // BFS on 4-neighborhood, blocking through occupied tiles; returns empty, in-bounds, reachable cells within speed steps.
        var result = new HashSet<Coord>();
        if (speed <= 0) return result;
        var src = (Coord)state.Units[pid].Vars[Keys.Pos];
        var q = new Queue<(Coord pos, int dist)>();
        var visited = new HashSet<Coord>();
        q.Enqueue((src, 0));
        visited.Add(src);
        int[,] dirs = new int[4,2] { {1,0},{-1,0},{0,1},{0,-1} };
        while (q.Count > 0)
        {
            var (cur, d) = q.Dequeue();
            if (d == speed) continue;
            for (int i = 0; i < 4; i++)
            {
                var nx = cur.X + dirs[i,0];
                var ny = cur.Y + dirs[i,1];
                var np = new Coord(nx, ny);
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                if (visited.Contains(np)) continue;
                // cannot pass through occupied tiles
                if (Occupied(np)) continue;
                visited.Add(np);
                q.Enqueue((np, d + 1));
                // do not include source; only free reachable cells
                result.Add(np);
            }
        }
        return result;
    }

    private HashSet<Coord> EnemiesInRange(string pid, int range)
    {
        var set = new HashSet<Coord>();
        var myPos = (Coord)state.Units[pid].Vars[Keys.Pos];
        foreach (var (id, u) in state.Units)
        {
            if (id == pid) continue;
            if (GetInt(id, Keys.Hp, 0) <= 0) continue;
            var pos = (Coord)u.Vars[Keys.Pos];
            var d = Math.Abs(pos.X - myPos.X) + Math.Abs(pos.Y - myPos.Y);
            if (d <= range) set.Add(pos);
        }
        return set;
    }
}
