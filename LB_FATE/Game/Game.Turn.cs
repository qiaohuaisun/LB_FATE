using ETBBS;

namespace LB_FATE;

partial class Game
{
    private void Turn(string pid, int phase, int day)
    {
        // Expose current phase to core engine for phase-dependent talents
        state = WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars.SetItem(DslRuntime.PhaseKey, phase) });
        Context ctx;
        // speed/range/rooted/silenced will be recomputed dynamically each loop
        IPlayerEndpoint? ep = endpoints.TryGetValue(pid, out var ept) ? ept : null;

        // Enable auto-completion for ConsoleEndpoint
        if (ep is ConsoleEndpoint consoleEp)
        {
            var autoComplete = new AutoComplete(this, pid);
            consoleEp.SetAutoCompleteReader(() => InputReader.ReadLineWithCompletion(autoComplete));
        }

        while (true)
        {
            // 若连接已关闭：移除并广播离线
            if (ep is not null && !ep.IsAlive)
            {
                endpoints.Remove(pid);
                foreach (var kv in endpoints) kv.Value.SendLine($"玩家离线：{pid}");
                break;
            }
            // refresh context/dynamic stats each loop to reflect latest state after previous actions
            ctx = new Context(state);
            var speed = GetInt(pid, Keys.Speed, 3);
            var range = GetInt(pid, Keys.Range, 1);
            // skip turn if unit doesn't exist or cannot act
            if (!state.Units.ContainsKey(pid))
            {
                break;
            }
            if (GetInt(pid, Keys.CannotActTurns, 0) > 0)
            {
                if (ep is not null) WriteLineTo(pid, "You are incapacitated and cannot act this phase.");
                else AppendPublic(new[] { $"{pid} cannot act this phase." });
                break;
            }
            bool isRooted = state.Units[pid].Tags.Contains(Tags.Rooted);
            bool isSilenced = state.Units[pid].Tags.Contains(Tags.Silenced);
            if (ep is not null)
            {
                ep.SendLine("PROMPT");
                var line = ep.ReadLine() ?? string.Empty;
                if (!ep.IsAlive)
                {
                    endpoints.Remove(pid);
                    foreach (var kv in endpoints) kv.Value.SendLine($"玩家离线：{pid}");
                    break;
                }
                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var cmd = parts[0].ToLowerInvariant();

                if (cmd is "help" or "h")
                {
                    WriteLineTo(pid, $"move x y | m x y : move to a reachable tile (clear path ≤ speed, cost: {GameConstants.MovementCost} MP)");
                    WriteLineTo(pid, $"attack P# | a P# : attack target (cost: {GameConstants.BasicAttackCost} MP; LBR Basic Attack if exists)");
                    WriteLineTo(pid, "skills | s       : list available skills");
                    WriteLineTo(pid, "info | i         : show role description");
                    WriteLineTo(pid, "use n P# | u n P#: cast skill #n (target optional by targeting)");
                    WriteLineTo(pid, "hint move|hm     : highlight reachable tiles");
                    WriteLineTo(pid, "pass | p         : end your turn");
                    WriteLineTo(pid, $"Costs           : Move {GameConstants.MovementCost} MP; Attack {GameConstants.BasicAttackCost} MP");
                    WriteLineTo(pid, "Note            : Each player acts once per phase.");
                    WriteLineTo(pid, "Tip             : Press TAB for auto-completion");
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
                if (cmd is "hint" or "hm")
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
                    WriteLineTo(pid, "Usage: hint move"); continue;
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
                    // MP cost for moving
                    var mpObj0 = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
                    double mp0 = mpObj0 is double dd0 ? dd0 : (mpObj0 is int ii0 ? ii0 : 0);
                    double moveCost = GameConstants.MovementCost;
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
                    AppendDebugFor(pid, log.Messages);
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
                    var skill = role.Skills[idx];

                    // Parse target argument robustly: allow unit id OR coordinates OR direction.
                    // We only treat the 3rd token as a unit id if it matches an existing unit; otherwise try x y or direction.
                    string? tid = null;
                    var casterPos = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
                    Coord point = new Coord(Math.Clamp(casterPos.X, 0, width - 1), Math.Max(0, casterPos.Y - 1));
                    string dir = string.Empty;
                    bool hasPointArg = false;

                    if (parts.Length >= 4 && int.TryParse(parts[2], out var px) && int.TryParse(parts[3], out var py))
                    {
                        // Explicit coordinates: use as $point
                        point = new Coord(Math.Clamp(px, 0, width - 1), Math.Clamp(py, 0, height - 1));
                        hasPointArg = true;
                    }
                    else if (parts.Length >= 3)
                    {
                        var arg = parts[2];
                        var argUpper = arg.ToUpperInvariant();
                        var argLower = arg.ToLowerInvariant();

                        // Directional hint with optional steps
                        if (argLower is "up" or "down" or "left" or "right")
                        {
                            int steps = 1;
                            if (parts.Length >= 4 && int.TryParse(parts[3], out var st) && st > 0) steps = st;
                            steps = Math.Max(1, Math.Min(Math.Max(width, height), steps));
                            if (argLower == "up") { dir = "up"; point = new Coord(casterPos.X, Math.Max(0, casterPos.Y - steps)); }
                            else if (argLower == "down") { dir = "down"; point = new Coord(casterPos.X, Math.Min(height - 1, casterPos.Y + steps)); }
                            else if (argLower == "left") { dir = "left"; point = new Coord(Math.Max(0, casterPos.X - steps), casterPos.Y); }
                            else if (argLower == "right") { dir = "right"; point = new Coord(Math.Min(width - 1, casterPos.X + steps), casterPos.Y); }
                            hasPointArg = true;

                            // For skills that need a unit target (not tile-based), find the nearest unit in that direction
                            string skillTargeting = skill.Compiled.Extras.TryGetValue("targeting", out var skillTgtObj) && skillTgtObj is string sts ? sts : "any";
                            if (skillTargeting != "tile" && skill.Compiled.Metadata.Range > 0)
                            {
                                // Find nearest unit in the direction
                                string? nearestInDir = null;
                                int bestDist = int.MaxValue;
                                foreach (var (uid, u) in state.Units)
                                {
                                    if (uid == pid) continue;
                                    if (GetInt(uid, Keys.Hp, 0) <= 0) continue;
                                    var uPos = ctx.GetUnitVar<Coord>(uid, Keys.Pos, default);
                                    // Check if unit is in the specified direction
                                    bool inDirection = argLower switch
                                    {
                                        "up" => uPos.X == casterPos.X && uPos.Y < casterPos.Y,
                                        "down" => uPos.X == casterPos.X && uPos.Y > casterPos.Y,
                                        "left" => uPos.Y == casterPos.Y && uPos.X < casterPos.X,
                                        "right" => uPos.Y == casterPos.Y && uPos.X > casterPos.X,
                                        _ => false
                                    };
                                    if (inDirection)
                                    {
                                        int dist = Math.Abs(uPos.X - casterPos.X) + Math.Abs(uPos.Y - casterPos.Y);
                                        if (dist < bestDist)
                                        {
                                            bestDist = dist;
                                            nearestInDir = uid;
                                        }
                                    }
                                }
                                if (nearestInDir != null)
                                {
                                    tid = nearestInDir;
                                }
                            }
                        }
                        else if (state.Units.ContainsKey(argUpper))
                        {
                            // Valid unit id
                            tid = argUpper;
                            if (GetInt(tid, Keys.Hp, 0) <= 0) { WriteLineTo(pid, "Invalid target."); continue; }
                        }
                        else
                        {
                            // Not a unit, not direction, and not coordinates → invalid
                            WriteLineTo(pid, "Invalid target.");
                            continue;
                        }
                    }

                    // Check if skill requires a target but none was provided
                    string targeting = skill.Compiled.Extras.TryGetValue("targeting", out var tgtObj) && tgtObj is string ts ? ts : "any";
                    int skillRange = skill.Compiled.Metadata.Range;

                    // For range 0 skills with targeting "self", auto-target self if no arguments provided
                    if (skillRange == 0 && targeting == "self" && string.IsNullOrEmpty(tid) && !hasPointArg)
                    {
                        tid = pid;
                    }

                    // Skills with range > 0 and targeting != self/tile require a target or direction
                    if (targeting != "self" && targeting != "tile" && skillRange > 0 && string.IsNullOrEmpty(tid) && !hasPointArg)
                    {
                        WriteLineTo(pid, $"Skill '{skill.Name}' requires a target. Usage: use {idx} <target|x y|up|down|left|right>");
                        continue;
                    }
                    state = WorldStateOps.WithGlobal(state, g => g with
                    {
                        Vars = g.Vars
                        .SetItem(DslRuntime.CasterKey, pid)
                        .SetItem(DslRuntime.TargetKey, tid ?? "")
                        .SetItem(DslRuntime.RngKey, rng)
                        .SetItem(DslRuntime.TeamsKey, teamOf)
                        .SetItem(DslRuntime.TargetPointKey, point)
                        .SetItem(DslRuntime.DirKey, dir)
                    });
                    var cfg = new ActionValidationConfig(
                        CasterId: pid,
                        TargetUnitId: tid,
                        TargetPos: hasPointArg ? point : null,
                        TeamOfUnit: teamOf,
                        Targeting: TargetingMode.Any,
                        CurrentTurn: state.Global.Turn,
                        CurrentDay: day,
                        CurrentPhase: phase
                    );
                    var validator = ActionValidators.ForSkillWithExtras(skill.Compiled, cfg, cooldowns);
                    var se = new SkillExecutor();
                    (state, var log) = se.ExecutePlan(state, skill.Compiled.BuildPlan(new Context(state)), validator);
                    AppendDebugFor(pid, log.Messages);
                    BroadcastBoard(day, phase);
                    cooldowns.SetLastUseTurn(pid, skill.Name, state.Global.Turn);
                    highlightCells = null;
                    // If skill is marked as ending the turn, break the command loop
                    if (skill.Compiled.Extras.TryGetValue("ends_turn", out var et) && et is bool b && b)
                        break;
                    // If inspection outputs exist, log and clear them
                    object? ihp = null, imp = null, ipos = null;
                    bool gotHp = state.Global.Vars.TryGetValue("inspect_hp", out ihp);
                    bool gotMp = state.Global.Vars.TryGetValue("inspect_mp", out imp);
                    bool gotPos = state.Global.Vars.TryGetValue("inspect_pos", out ipos);
                    if (gotHp || gotMp || gotPos)
                    {
                        int hpV = ihp is int hpi ? hpi : (ihp is long hpl ? (int)hpl : (ihp is double hpd ? (int)Math.Round(hpd) : 0));
                        double mpV = imp is double dmp ? dmp : (imp is int impi ? impi : 0);
                        string posV = ipos is Coord pc ? pc.ToString() : "(?,?)";
                        WriteLineTo(pid, $"Inspect: HP={hpV}, MP={mpV:0.##}, Pos={posV}");
                        // Also show target role/class and a short description (self-only)
                        if (!string.IsNullOrWhiteSpace(tid))
                        {
                            try
                            {
                                if (roleOf.ContainsKey(tid))
                                {
                                    var r = roleOf[tid];
                                    WriteLineTo(pid, $"Inspect: Role={r.Name} ({r.Id})");
                                    var desc = r.Description ?? string.Empty;
                                    var firstLine = desc.Split('\n').Select(s => s.Trim()).FirstOrDefault(s => !string.IsNullOrEmpty(s));
                                    if (!string.IsNullOrEmpty(firstLine))
                                        WriteLineTo(pid, $"Desc: {firstLine}");
                                }
                                else if (classOf.ContainsKey(tid))
                                {
                                    WriteLineTo(pid, $"Inspect: Class={classOf[tid]}");
                                }
                            }
                            catch (Exception ex)
                            {
                                // Non-critical: inspection display failed, but continue game
                                Console.WriteLine($"Warning: Failed to display inspect info for {tid}: {ex.Message}");
                            }
                        }
                        state = WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars.Remove("inspect_hp").Remove("inspect_mp").Remove("inspect_pos") });
                    }
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
                        state = WorldStateOps.WithGlobal(state, g => g with
                        {
                            Vars = g.Vars
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
                            CurrentTurn: state.Global.Turn,
                            CurrentDay: day,
                            CurrentPhase: phase
                        );
                        var validator2 = ActionValidators.ForSkillWithExtras(basic.Compiled, cfg2, cooldowns);
                        var se2 = new SkillExecutor();
                        // 普攻统一消耗 MP
                        var mpObjB = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
                        double mpB = mpObjB is double ddb ? ddb : (mpObjB is int ib ? ib : 0);
                        double basicCost = GameConstants.BasicAttackCost;
                        if (mpB < basicCost) { WriteLineTo(pid, "Not enough MP."); continue; }
                        int repeats = (d <= twinRange && extraStrikes > 1) ? extraStrikes : 1;
                        for (int i = 0; i < repeats; i++)
                        {
                            (state, var log) = se2.ExecutePlan(state, basic.Compiled.BuildPlan(new Context(state)), validator2);
                            AppendDebugFor(pid, log.Messages);
                        }
                        (state, var log2) = se2.Execute(state, new AtomicAction[] { new ModifyUnitVar(pid, Keys.Mp, v => (v is double d0 ? d0 : Convert.ToDouble(v)) - basicCost) });
                        AppendDebugFor(pid, log2.Messages);
                        BroadcastBoard(day, phase);
                        // If basic attack had ends_turn (unlikely), honor it
                        if (basic.Compiled.Extras.TryGetValue("ends_turn", out var et2) && et2 is bool bb && bb)
                            break;
                        cooldowns.SetLastUseTurn(pid, basic.Name, state.Global.Turn);
                    }
                    else
                    {
                        if (d > range) { WriteLineTo(pid, $"Target out of range ({range})."); continue; }
                        var actions = new List<AtomicAction>();
                        // 普攻统一消耗：所有阶职 MP
                        var mpObj = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
                        double mp = mpObj is double dd ? dd : (mpObj is int i2 ? i2 : 0);
                        double cost = GameConstants.BasicAttackCost;
                        if (mp < cost) { WriteLineTo(pid, "Not enough MP."); continue; }
                        actions.Add(new ModifyUnitVar(pid, Keys.Mp, v => (v is double d0 ? d0 : Convert.ToDouble(v)) - cost));
                        var power = 5;
                        int repeats2 = (d <= twinRange && extraStrikes > 1) ? extraStrikes : 1;
                        for (int i = 0; i < repeats2; i++) actions.Add(new PhysicalDamage(pid, tid, power, 0.0));
                        var se3 = new SkillExecutor();
                        (state, var log) = se3.Execute(state, actions.ToArray());
                        AppendDebugFor(pid, log.Messages);
                        BroadcastBoard(day, phase);
                    }
                    highlightCells = null;
                    continue;
                }
                WriteLineTo(pid, "Unknown command. Type 'help'.");
            }
            else
            {
                // No endpoint: if this is the AI boss, perform AI script (if any) or fallback heuristic; otherwise, end turn.
                if (bossMode && pid == bossId)
                {
                    // Broadcast turn start banner (not added to log to avoid duplication)
                    BroadcastBanner("", "╔═══════════════════════════════════════════════════════════════╗", $"║  ⚔️  【{bossName}】的回合开始  ⚔️  ", "╚═══════════════════════════════════════════════════════════════╝", "");
                    // Play turn start quote
                    if (roleOf.TryGetValue(pid, out var bossRole))
                    {
                        var quote = ETBBS.RoleQuotes.GetRandom(bossRole.Quotes.OnTurnStart, rng);
                        if (!string.IsNullOrEmpty(quote))
                        {
                            AppendPublic(new[] { $"💬 【{bossName}】：\"{quote}\"" });
                        }
                    }
                    BroadcastBoard(day, phase);
                    bool done = TryExecuteBossAiScript(pid, phase, day);
                    if (!done) RunBossAiTurn(pid, phase, day);
                    // Check HP threshold quotes after actions
                    CheckHpThresholdQuotes(pid);
                    // Play turn end quote
                    if (roleOf.TryGetValue(pid, out bossRole))
                    {
                        var quote = ETBBS.RoleQuotes.GetRandom(bossRole.Quotes.OnTurnEnd, rng);
                        if (!string.IsNullOrEmpty(quote))
                        {
                            AppendPublic(new[] { $"💬 【{bossName}】：\"{quote}\"" });
                        }
                    }
                    // Broadcast turn end banner
                    BroadcastBanner("", "╔═══════════════════════════════════════════════════════════════╗", $"║  ⚔️  【{bossName}】的回合结束  ⚔️  ", "╚═══════════════════════════════════════════════════════════════╝", "");
                    BroadcastBoard(day, phase);
                }
                break;
            }
        }
    }

    private int EstimateLineAoeHits(string attackerId, string targetId, int length, int radius)
    {
        var ctx = new Context(state);
        if (!state.Units.TryGetValue(attackerId, out var au) || !state.Units.TryGetValue(targetId, out var tu)) return 0;
        if (!au.Vars.TryGetValue(Keys.Pos, out var ap) || ap is not Coord aPos) return 0;
        if (!tu.Vars.TryGetValue(Keys.Pos, out var tp) || tp is not Coord tPos) return 0;
        int steps = Math.Max(0, length);
        int rad = Math.Max(0, radius);
        int dx = Math.Sign(tPos.X - aPos.X);
        int dy = Math.Sign(tPos.Y - aPos.Y);
        var path = new List<Coord>();
        var cur = aPos;
        for (int i = 0; i < steps; i++)
        {
            if (cur.Equals(tPos)) break;
            Coord next = cur;
            if (cur.X != tPos.X) next = new Coord(cur.X + dx, cur.Y);
            else if (cur.Y != tPos.Y) next = new Coord(cur.X, cur.Y + dy);
            cur = next; path.Add(cur);
        }
        // teams map
        string? atkTeam = teamOf.TryGetValue(attackerId, out var tteam) ? tteam : null;
        int hits = 0;
        foreach (var (id, u) in state.Units)
        {
            if (id == attackerId) continue;
            if (teamOf.TryGetValue(id, out var tt) && atkTeam is not null && tt == atkTeam) continue;
            if (GetInt(id, Keys.Hp, 0) <= 0) continue;
            if (!u.Vars.TryGetValue(Keys.Pos, out var pv) || pv is not Coord pos) continue;
            bool inRange = path.Any(c => Math.Abs(c.X - pos.X) + Math.Abs(c.Y - pos.Y) <= rad);
            if (inRange) hits++;
        }
        return hits;
    }

    private bool TryExecuteBossAiScript(string pid, int phase, int day)
    {
        // If a telegraph is pending and due, resolve it first
        try
        {
            if (state.Global.Vars.TryGetValue("boss_telegraph", out var tele) && tele is string payload)
            {
                var parts = payload.Split('|');
                // pid|skill|day|phase|tid|px|py|msg
                if (parts.Length >= 8)
                {
                    var pid0 = parts[0]; var skillName = parts[1];
                    int d = int.Parse(parts[2]); int ph = int.Parse(parts[3]);
                    var tid = string.IsNullOrWhiteSpace(parts[4]) ? null : parts[4];
                    int px = int.Parse(parts[5]); int py = int.Parse(parts[6]);
                    var msg = parts[7];
                    if (pid0 == pid && (day > d || (day == d && phase >= ph)))
                    {
                        var role = roleOf.ContainsKey(pid) ? roleOf[pid] : null; if (role is null) return false;
                        var s = role.Skills.FirstOrDefault(x => string.Equals(x.Name, skillName, StringComparison.OrdinalIgnoreCase));
                        if (s is not null)
                        {
                            var point = new Coord(Math.Clamp(px, 0, width - 1), Math.Clamp(py, 0, height - 1));
                            // prepare baseline globals
                            state = WorldStateOps.WithGlobal(state, g => g with
                            {
                                Vars = g.Vars
                                    .SetItem(DslRuntime.CasterKey, pid)
                                    .SetItem(DslRuntime.TargetKey, tid ?? "")
                                    .SetItem(DslRuntime.RngKey, rng)
                                    .SetItem(DslRuntime.TeamsKey, teamOf)
                                    .SetItem(DslRuntime.TargetPointKey, point)
                                    .Remove("boss_telegraph").Remove("boss_telegraph_msg")
                            });
                            var cfg = new ActionValidationConfig(
                                CasterId: pid, TargetUnitId: tid, TeamOfUnit: teamOf, Targeting: TargetingMode.Any,
                                CurrentTurn: state.Global.Turn, CurrentDay: day, CurrentPhase: phase, TargetPos: point);
                            var validator = ActionValidators.ForSkillWithExtras(s.Compiled, cfg, cooldowns);
                            var plan = s.Compiled.BuildPlan(new Context(state));
                            var batch = plan.Count > 0 ? plan[0] : Array.Empty<AtomicAction>();
                            // If validation fails due to target invalidation, try fallbacks
                            if (!validator(new Context(state), batch, out var _))
                            {
                                // Determine targeting mode
                                string targeting = s.Compiled.Extras.TryGetValue("targeting", out var tv) && tv is string ts ? ts : "any";
                                // Find nearest alive enemy
                                string? newTid = null; Coord myPos = new Context(state).GetUnitVar<Coord>(pid, Keys.Pos, default); int bestD2 = int.MaxValue;
                                foreach (var (id, u2) in state.Units)
                                {
                                    if (id == pid) continue; if (GetInt(id, Keys.Hp, 0) <= 0) continue;
                                    if (teamOf.TryGetValue(id, out var t2) && teamOf.TryGetValue(pid, out var tb2) && t2 == tb2) continue;
                                    var p2 = new Context(state).GetUnitVar<Coord>(id, Keys.Pos, default);
                                    int dd = Math.Abs(p2.X - myPos.X) + Math.Abs(p2.Y - myPos.Y);
                                    if (dd < bestD2) { bestD2 = dd; newTid = id; }
                                }
                                if (targeting == "tile")
                                {
                                    // choose a tile within range nearest to nearest enemy
                                    int range = s.Compiled.Metadata.Range;
                                    Coord best = point; int bestScore = int.MaxValue;
                                    var targetPos = (newTid is null) ? point : new Context(state).GetUnitVar<Coord>(newTid, Keys.Pos, default);
                                    for (int dx = -range; dx <= range; dx++)
                                    for (int dy = -range; dy <= range; dy++)
                                    {
                                        int md = Math.Abs(dx) + Math.Abs(dy); if (md > range) continue;
                                        var c = new Coord(Math.Clamp(myPos.X + dx, 0, width - 1), Math.Clamp(myPos.Y + dy, 0, height - 1));
                                        int score = Math.Abs(c.X - targetPos.X) + Math.Abs(c.Y - targetPos.Y);
                                        if (score < bestScore) { bestScore = score; best = c; }
                                    }
                                    point = best; tid = newTid; // prefer proximity
                                }
                                else
                                {
                                    // unit-targeting or self/any -> prefer best line-aoe target if applicable, else nearest enemy id
                                    // Try extract line aoe params from current plan
                                    int lineLen = 0, lineRad = 0; bool hasLine = false;
                                    foreach (var b in plan)
                                    {
                                        foreach (var a in b)
                                        {
                                            if (a is LineAoeDamage lad && lad.AttackerId == pid)
                                            {
                                                lineLen = lad.Length; lineRad = lad.Radius; hasLine = true; break;
                                            }
                                        }
                                        if (hasLine) break;
                                    }
                                    if (hasLine)
                                    {
                                        string? bestId = null; int bestHits = -1; int bestTieDist = int.MaxValue;
                                        foreach (var (id, u2) in state.Units)
                                        {
                                            if (id == pid) continue; if (GetInt(id, Keys.Hp, 0) <= 0) continue;
                                            if (teamOf.TryGetValue(id, out var t2) && teamOf.TryGetValue(pid, out var tb2) && t2 == tb2) continue;
                                            int hits = EstimateLineAoeHits(pid, id, lineLen, lineRad);
                                            var p2 = new Context(state).GetUnitVar<Coord>(id, Keys.Pos, default);
                                            int dist = Math.Abs(p2.X - myPos.X) + Math.Abs(p2.Y - myPos.Y);
                                            if (hits > bestHits || (hits == bestHits && dist < bestTieDist))
                                            {
                                                bestHits = hits; bestTieDist = dist; bestId = id;
                                            }
                                        }
                                        if (bestId is not null) tid = bestId; else if (newTid is not null) tid = newTid;
                                    }
                                    else
                                    {
                                        if (newTid is not null) tid = newTid;
                                    }
                                }
                                // update globals + cfg with fallback
                                state = WorldStateOps.WithGlobal(state, g => g with
                                {
                                    Vars = g.Vars
                                        .SetItem(DslRuntime.CasterKey, pid)
                                        .SetItem(DslRuntime.TargetKey, tid ?? "")
                                        .SetItem(DslRuntime.TargetPointKey, point)
                                });
                                cfg = cfg with { TargetUnitId = tid, TargetPos = point };
                                validator = ActionValidators.ForSkillWithExtras(s.Compiled, cfg, cooldowns);
                                plan = s.Compiled.BuildPlan(new Context(state));
                            }
                            var se = new SkillExecutor();
                            AppendPublic(new[] { $"⚡ {msg} -> 已释放" });

                            (state, var log) = se.ExecutePlan(state, plan, validator);
                            AppendDebugFor(pid, log.Messages);

                            BroadcastBoard(day, phase);
                            cooldowns.SetLastUseTurn(pid, s.Name, state.Global.Turn);
                            return true;
                        }
                    }
                }
            }
        }
        catch (FormatException ex)
        {
            Console.WriteLine($"Error: Boss AI telegraph data is corrupted: {ex.Message}");
            // Clear corrupted telegraph data
            state = WorldStateOps.WithGlobal(state, g => g with
            {
                Vars = g.Vars.Remove("boss_telegraph").Remove("boss_telegraph_msg")
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Unexpected error in boss telegraph execution: {ex.GetType().Name} - {ex.Message}");
        }

        if (bossAi is null) return false;
        try
        {
            var ctx = new Context(state);
            // compute nearest enemy
            string? nearestId = null; int bestD = int.MaxValue; Coord myPos = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
            foreach (var (id, u) in state.Units)
            {
                if (id == pid) continue; if (GetInt(id, Keys.Hp, 0) <= 0) continue;
                if (teamOf.TryGetValue(id, out var t) && teamOf.TryGetValue(pid, out var tb) && t == tb) continue;
                var pos = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
                int d = Math.Abs(pos.X - myPos.X) + Math.Abs(pos.Y - myPos.Y);
                if (d < bestD) { bestD = d; nearestId = id; }
            }
            var role = roleOf.ContainsKey(pid) ? roleOf[pid] : null;
            bool telePending = false;
            try
            {
                telePending = state.Global.Vars.TryGetValue("boss_telegraph", out var tv) && tv is string s && s.StartsWith(pid + "|");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to check telegraph status for {pid}: {ex.Message}");
                telePending = false;
            }

            foreach (var rule in bossAi.Rules)
            {
                // Skip scheduling new telegraphs if one is already pending
                if (telePending && (rule.Telegraph ?? false)) continue;
                // Chance gate
                if (rule.Chance is double pc && pc >= 0.0 && pc <= 1.0)
                {
                    var rv = rng.NextDouble(); if (rv > pc) continue;
                }
                if (!RuleMatches(rule, pid, role, nearestId, bestD, phase)) continue;
                var ok = ExecuteRule(rule, pid, role, nearestId, day, phase);
                if (ok) return true;
            }
            // fallback
            if (string.Equals(bossAi.Fallback, "basic_attack", StringComparison.OrdinalIgnoreCase))
            {
                return RunBasicAttackFallback(pid, nearestId, day, phase);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Boss AI execution failed for {pid}: {ex.GetType().Name} - {ex.Message}");
            return false;
        }

        return false;
    }

    private bool RuleMatches(BossAiRule rule, string pid, RoleDefinition? role, string? nearestId, int nearestDist, int phase)
    {
        var cond = rule.If; if (cond is null) return true;
        var u = state.Units[pid];
        if (cond.HpPctLte is double hpPct)
        {
            int hp = GetInt(pid, Keys.Hp, 0); int maxHp = GetInt(pid, Keys.MaxHp, Math.Max(1, hp));
            if (hp > (int)Math.Round(maxHp * Math.Clamp(hpPct, 0.0, 1.0))) return false;
        }
        if (cond.MinMp is double mmp)
        {
            var mpObj = u.Vars.TryGetValue(Keys.Mp, out var mv) ? mv : 0.0;
            double mp = mpObj is double dd ? dd : (mpObj is int ii ? ii : 0.0);
            if (mp < mmp) return false;
        }
        if (cond.PhaseIn is int[] ph && ph.Length > 0)
        {
            bool allowed = ph.Contains(phase);
            if (!allowed) return false;
        }
        if (!string.IsNullOrEmpty(cond.HasTag))
        {
            if (!u.Tags.Contains(cond.HasTag)) return false;
        }
        if (cond.DistanceLte is int dl)
        {
            if (nearestId is null) return false; if (nearestDist > dl) return false;
        }
        if (!string.IsNullOrEmpty(cond.TargetHasTag))
        {
            if (nearestId is null) return false;
            if (!state.Units[nearestId].Tags.Contains(cond.TargetHasTag)) return false;
        }
        if (!string.IsNullOrEmpty(cond.SkillReady))
        {
            if (role is null) return false;
            var s = role.Skills.FirstOrDefault(x => string.Equals(x.Name, cond.SkillReady, StringComparison.OrdinalIgnoreCase));
            if (s is null) return false;
            // minimal readiness: MP/CD only (range may be validated by target)
            var cfg = new ActionValidationConfig(
                CasterId: pid,
                TeamOfUnit: teamOf,
                Targeting: TargetingMode.Any,
                CurrentTurn: state.Global.Turn,
                CurrentDay: state.Global.Vars.TryGetValue(DslRuntime.PhaseKey, out var _) ? state.Global.Turn : state.Global.Turn,
                CurrentPhase: phase
            );
            var val = ActionValidators.ForSkillWithExtras(s.Compiled, cfg, cooldowns);
            var plan = s.Compiled.BuildPlan(new Context(state));
            var batch = plan.Count > 0 ? plan[0] : Array.Empty<AtomicAction>();
            if (!val(new Context(state), batch, out var _)) return false;
        }
        if (cond.MinHits is int need && need > 0)
        {
            // ballpark estimate: cluster radius from target.radius
            int rad = rule.Target?.Radius ?? 0;
            int hits = EstimateClusterHits(pid, rad);
            if (hits < need) return false;
        }
        return true;
    }

    private int EstimateClusterHits(string pid, int radius)
    {
        var ctx = new Context(state);
        var p = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
        int best = 0;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var c = new Coord(Math.Clamp(p.X + dx, 0, width - 1), Math.Clamp(p.Y + dy, 0, height - 1));
                int count = 0;
                foreach (var (id, u) in state.Units)
                {
                    if (id == pid) continue; if (GetInt(id, Keys.Hp, 0) <= 0) continue;
                    if (teamOf.TryGetValue(id, out var t) && teamOf.TryGetValue(pid, out var tb) && t == tb) continue;
                    var pos = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
                    int d = Math.Max(Math.Abs(pos.X - c.X), Math.Abs(pos.Y - c.Y));
                    if (d <= radius) count++;
                }
                if (count > best) best = count;
            }
        }
        return best;
    }

    private bool ExecuteRule(BossAiRule rule, string pid, RoleDefinition? role, string? nearestId, int day, int phase)
    {
        string action = rule.Action ?? "cast";
        if (action.Equals("move_to", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteMoveTo(rule, pid, nearestId, day, phase);
        }
        if (action.Equals("retreat", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteRetreat(rule, pid, nearestId, day, phase);
        }
        if (action.Equals("basic_attack", StringComparison.OrdinalIgnoreCase))
        {
            return RunBasicAttackFallback(pid, nearestId, day, phase);
        }
        if (role is null) return false;
        string? skillName = rule.Skill ?? rule.If?.SkillReady;
        if (string.IsNullOrEmpty(skillName)) return false;
        var s = role.Skills.FirstOrDefault(x => string.Equals(x.Name, skillName, StringComparison.OrdinalIgnoreCase));
        if (s is null) return false;

        // target resolution
        string? tid = nearestId;
        Coord point = new Coord(); bool usePoint = false; string dir = string.Empty;
        if (rule.Target is BossAiTarget tgt)
        {
            if (tgt.Type.Equals("cluster", StringComparison.OrdinalIgnoreCase))
            {
                int radius = tgt.Radius ?? 1; // find best cluster tile around caster
                var ctx = new Context(state); var p0 = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
                int best = -1; Coord bestC = p0;
                for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var c = new Coord(Math.Clamp(p0.X + dx, 0, width - 1), Math.Clamp(p0.Y + dy, 0, height - 1));
                    int count = 0;
                    foreach (var (id, u) in state.Units)
                    {
                        if (id == pid) continue; if (GetInt(id, Keys.Hp, 0) <= 0) continue;
                        if (teamOf.TryGetValue(id, out var t) && teamOf.TryGetValue(pid, out var tb) && t == tb) continue;
                        var pos = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
                        int d = Math.Max(Math.Abs(pos.X - c.X), Math.Abs(pos.Y - c.Y));
                        if (d <= radius) count++;
                    }
                    if (count > best) { best = count; bestC = c; }
                }
                point = bestC; usePoint = true;
            }
            else if (tgt.Type.Equals("nearest_enemy", StringComparison.OrdinalIgnoreCase))
            {
                // choose by prefer_tag/order_var if provided
                var enemies = state.Units.Where(kv => kv.Key != pid && GetInt(kv.Key, Keys.Hp, 0) > 0 && (!teamOf.TryGetValue(kv.Key, out var t) || !teamOf.TryGetValue(pid, out var tb) || t != tb)).Select(kv => kv.Key);
                if (!string.IsNullOrEmpty(tgt.PreferTag))
                {
                    var tagged = enemies.Where(id => state.Units[id].Tags.Contains(tgt.PreferTag)).ToList();
                    if (tagged.Count > 0) enemies = tagged;
                }
                if (!string.IsNullOrEmpty(tgt.OrderVarKey))
                {
                    string key = tgt.OrderVarKey!;
                    enemies = (tgt.OrderVarDesc ? enemies.OrderByDescending(id => new Context(state).GetUnitVar<int>(id, key, int.MinValue))
                                                : enemies.OrderBy(id => new Context(state).GetUnitVar<int>(id, key, int.MaxValue)));
                }
                tid = enemies.FirstOrDefault() ?? tid;
            }
        }

        // Telegraph support: if rule requires telegraph, announce now and schedule execution next phase
        if (rule.Telegraph is bool tg && tg)
        {
            int delay = Math.Max(1, rule.TelegraphDelay ?? 1);
            int totalPhases = 5;
            int curIndex = (day - 1) * totalPhases + (phase - 1); // zero-based
            int execIndex = curIndex + delay;
            int nextDay = (execIndex / totalPhases) + 1;
            int nextPhase = (execIndex % totalPhases) + 1;
            var msg = rule.TelegraphMessage ?? (usePoint ? $"Boss 预警：{skillName} 将落在 {point}" : $"Boss 预警：{skillName} 将对 {tid ?? "?"} 释放");
            var payload = string.Join('|', new string[]
            {
                pid,
                skillName,
                nextDay.ToString(),
                nextPhase.ToString(),
                tid ?? string.Empty,
                (usePoint ? point.X : 0).ToString(),
                (usePoint ? point.Y : 0).ToString(),
                msg
            });
            state = WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars.SetItem("boss_telegraph", payload).SetItem("boss_telegraph_msg", msg) });
            AppendPublic(new[] { $"⚠️  {msg}" });
            BroadcastBoard(day, phase);
            return true;
        }

        // prepare globals
        state = WorldStateOps.WithGlobal(state, g => g with
        {
            Vars = g.Vars
                .SetItem(DslRuntime.CasterKey, pid)
                .SetItem(DslRuntime.TargetKey, tid ?? "")
                .SetItem(DslRuntime.RngKey, rng)
                .SetItem(DslRuntime.TeamsKey, teamOf)
                .SetItem(DslRuntime.TargetPointKey, usePoint ? point : g.Vars.TryGetValue(DslRuntime.TargetPointKey, out var v) ? v : new Coord())
                .SetItem(DslRuntime.DirKey, dir)
        });
        var cfg = new ActionValidationConfig(
            CasterId: pid,
            TargetUnitId: tid,
            TeamOfUnit: teamOf,
            Targeting: TargetingMode.Any,
            CurrentTurn: state.Global.Turn,
            CurrentDay: day,
            CurrentPhase: phase,
            TargetPos: usePoint ? point : null
        );
        var validator = ActionValidators.ForSkillWithExtras(s.Compiled, cfg, cooldowns);
        var plan = s.Compiled.BuildPlan(new Context(state));
        var se = new SkillExecutor();

        string targetDesc = tid is not null ? tid : (usePoint ? $"{point}" : "无目标");

        // Play skill quote if available
        if (role != null && role.Quotes.OnSkill.TryGetValue(s.Name, out var skillQuotes))
        {
            var quote = ETBBS.RoleQuotes.GetRandom(skillQuotes, rng);
            if (!string.IsNullOrEmpty(quote))
            {
                AppendPublic(new[] { $"💬 【{bossName}】：\"{quote}\"" });
            }
        }

        AppendPublic(new[] { $"【{bossName}】{s.Name} → {targetDesc}" });

        (state, var log) = se.ExecutePlan(state, plan, validator);
        AppendDebugFor(pid, log.Messages);

        BroadcastBoard(day, phase);
        cooldowns.SetLastUseTurn(pid, s.Name, state.Global.Turn);
        return true;
    }

    private bool ExecuteMoveTo(BossAiRule rule, string pid, string? nearestId, int day, int phase)
    {
        if (nearestId is null) return false;
        var ctx = new Context(state);
        var speed = GetInt(pid, Keys.Speed, 3);
        var reachable = ReachableCells(pid, speed);
        if (reachable.Count == 0) return false;
        int desiredRange = GetInt(pid, Keys.Range, 1);
        var stopKey = rule.Target?.StopAtRangeOf;
        if (!string.IsNullOrEmpty(stopKey))
        {
            var role = roleOf.ContainsKey(pid) ? roleOf[pid] : null;
            var s = role?.Skills.FirstOrDefault(x => string.Equals(x.Name, stopKey, StringComparison.OrdinalIgnoreCase));
            if (s is not null) desiredRange = s.Compiled.Metadata.Range;
        }
        var tgPos = ctx.GetUnitVar<Coord>(nearestId, Keys.Pos, default);
        Coord best = default; int bestScore = int.MaxValue; // prefer within desiredRange, else closest
        foreach (var p in reachable)
        {
            int d = Math.Abs(p.X - tgPos.X) + Math.Abs(p.Y - tgPos.Y);
            int score = (d <= desiredRange) ? d : (1000 + d); // prioritize tiles within range
            if (score < bestScore) { bestScore = score; best = p; }
        }
        if (best.Equals(default(Coord))) return false;
        var mpObj0 = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
        double mp0 = mpObj0 is double dd0 ? dd0 : (mpObj0 is int ii0 ? ii0 : 0);
        double moveCost = GameConstants.MovementCost; if (mp0 < moveCost) return false;
        var se = new SkillExecutor();
        var curPos = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
        AppendPublic(new[] { $"【{bossName}】移动 {curPos} → {best}" });

        (state, var log) = se.Execute(state, new AtomicAction[] { new Move(pid, best), new ModifyUnitVar(pid, Keys.Mp, v => (v is double d ? d : Convert.ToDouble(v)) - moveCost) });
        AppendDebugFor(pid, log.Messages);
        BroadcastBoard(day, phase);
        return true;
    }

    private bool ExecuteRetreat(BossAiRule rule, string pid, string? nearestId, int day, int phase)
    {
        if (nearestId is null) return false;
        var ctx = new Context(state);
        var speed = GetInt(pid, Keys.Speed, 3);
        var reachable = ReachableCells(pid, speed);
        if (reachable.Count == 0) return false;
        var myPos = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
        var tgPos = ctx.GetUnitVar<Coord>(nearestId, Keys.Pos, default);
        Coord best = myPos; int bestDist = Math.Abs(myPos.X - tgPos.X) + Math.Abs(myPos.Y - tgPos.Y);
        foreach (var p in reachable)
        {
            int d = Math.Abs(p.X - tgPos.X) + Math.Abs(p.Y - tgPos.Y);
            if (d > bestDist) { bestDist = d; best = p; }
        }
        if (best.Equals(myPos)) return false;
        var mpObj0 = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
        double mp0 = mpObj0 is double dd0 ? dd0 : (mpObj0 is int ii0 ? ii0 : 0);
        double moveCost = GameConstants.MovementCost; if (mp0 < moveCost) return false;
        var se = new SkillExecutor();
        AppendPublic(new[] { $"【{bossName}】撤退 {myPos} → {best}" });

        (state, var log) = se.Execute(state, new AtomicAction[] { new Move(pid, best), new ModifyUnitVar(pid, Keys.Mp, v => (v is double d ? d : Convert.ToDouble(v)) - moveCost) });
        AppendDebugFor(pid, log.Messages);
        BroadcastBoard(day, phase);
        return true;
    }

    private bool RunBasicAttackFallback(string pid, string? nearestId, int day, int phase)
    {
        if (nearestId is null) return false;
        var ctx = new Context(state);
        var myPos2 = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
        var tgPos = ctx.GetUnitVar<Coord>(nearestId, Keys.Pos, default);
        var d = Math.Abs(myPos2.X - tgPos.X) + Math.Abs(myPos2.Y - tgPos.Y);
        var range = GetInt(pid, Keys.Range, 1);
        var roleLocal = roleOf.ContainsKey(pid) ? roleOf[pid] : null;
        var basic = roleLocal?.Skills.FirstOrDefault(s => s.Name == "Basic Attack");
        if (basic is null) return false;
        if (d > range) return false;
        var cfg2 = new ActionValidationConfig(
            CasterId: pid,
            TargetUnitId: nearestId,
            TeamOfUnit: teamOf,
            Targeting: TargetingMode.EnemiesOnly,
            CurrentTurn: state.Global.Turn,
            CurrentDay: day,
            CurrentPhase: phase
        );
        var validator2 = ActionValidators.ForSkillWithExtras(basic.Compiled, cfg2, cooldowns);
        var se2 = new SkillExecutor();
        var mpObj = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
        double mp = mpObj is double dd ? dd : (mpObj is int i2 ? i2 : 0);
        double cost = GameConstants.BasicAttackCost; if (mp < cost) return false;

        AppendPublic(new[] { $"【{bossName}】攻击 → {nearestId}" });

        (state, var log) = se2.ExecutePlan(state, basic.Compiled.BuildPlan(new Context(state)), validator2);
        AppendDebugFor(pid, log.Messages);

        (state, var log2) = se2.Execute(state, new AtomicAction[] { new ModifyUnitVar(pid, Keys.Mp, v => (v is double d0 ? d0 : Convert.ToDouble(v)) - cost) });
        AppendDebugFor(pid, log2.Messages);
        BroadcastBoard(day, phase);
        cooldowns.SetLastUseTurn(pid, basic.Name, state.Global.Turn);
        return true;
    }

    // --- Very simple Boss AI (MVP): use best in-range enemies-targeting skill; else basic; else move towards nearest enemy ---
    private void RunBossAiTurn(string pid, int phase, int day)
    {
        try
        {
            var ctx = new Context(state);
            // Find nearest enemy
            string? nearestId = null; int bestD = int.MaxValue; Coord myPos = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
            foreach (var (id, u) in state.Units)
            {
                if (id == pid) continue;
                if (GetInt(id, Keys.Hp, 0) <= 0) continue;
                if (teamOf.TryGetValue(id, out var t) && teamOf.TryGetValue(pid, out var tb) && t == tb) continue;
                var pos = ctx.GetUnitVar<Coord>(id, Keys.Pos, default);
                int d = Math.Abs(pos.X - myPos.X) + Math.Abs(pos.Y - myPos.Y);
                if (d < bestD) { bestD = d; nearestId = id; }
            }
            if (nearestId is null) return;

            var role = roleOf.ContainsKey(pid) ? roleOf[pid] : null;
            // Try enemies-targeting skills first (skip Basic Attack to keep it as fallback)
            if (role != null)
            {
                var skills = role.Skills;
                // Order by range descending
                var ordered = skills.OrderByDescending(s => s.Compiled.Metadata.Range).ToList();
                foreach (var s in ordered)
                {
                    if (s.Name == "Basic Attack") continue;
                    if (s.Compiled.Extras.TryGetValue("targeting", out var tv) && tv is string ts && ts == "enemies")
                    {
                        // Prepare runtime globals for DSL
                        state = WorldStateOps.WithGlobal(state, g => g with
                        {
                            Vars = g.Vars
                                .SetItem(DslRuntime.CasterKey, pid)
                                .SetItem(DslRuntime.TargetKey, nearestId)
                                .SetItem(DslRuntime.RngKey, rng)
                                .SetItem(DslRuntime.TeamsKey, teamOf)
                        });
                        var cfg = new ActionValidationConfig(
                            CasterId: pid,
                            TargetUnitId: nearestId,
                            TeamOfUnit: teamOf,
                            Targeting: TargetingMode.EnemiesOnly,
                            CurrentTurn: state.Global.Turn,
                            CurrentDay: day,
                            CurrentPhase: phase
                        );
                        var validator = ActionValidators.ForSkillWithExtras(s.Compiled, cfg, cooldowns);
                        var plan = s.Compiled.BuildPlan(new Context(state));
                        // Validate first batch
                        var batch = plan.Count > 0 ? plan[0] : Array.Empty<AtomicAction>();
                        if (validator(new Context(state), batch, out var _))
                        {
                            var se = new SkillExecutor();

                            // Play skill quote if available
                            if (role.Quotes.OnSkill.TryGetValue(s.Name, out var skillQuotes))
                            {
                                var quote = ETBBS.RoleQuotes.GetRandom(skillQuotes, rng);
                                if (!string.IsNullOrEmpty(quote))
                                {
                                    AppendPublic(new[] { $"💬 【{bossName}】：\"{quote}\"" });
                                }
                            }

                            AppendPublic(new[] { $"【{bossName}】{s.Name} → {nearestId}" });

                            (state, var log) = se.ExecutePlan(state, plan, validator);
                            AppendDebugFor(pid, log.Messages);

                            BroadcastBoard(day, phase);
                            cooldowns.SetLastUseTurn(pid, s.Name, state.Global.Turn);
                            return;
                        }
                    }
                }
            }

            // Fallback: Basic Attack if in range
            {
                var myPos2 = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
                var tgPos = ctx.GetUnitVar<Coord>(nearestId, Keys.Pos, default);
                var d = Math.Abs(myPos2.X - tgPos.X) + Math.Abs(myPos2.Y - tgPos.Y);
                var range = GetInt(pid, Keys.Range, 1);
                if (d <= range)
                {
                    var roleLocal = roleOf.ContainsKey(pid) ? roleOf[pid] : null;
                    var basic = roleLocal?.Skills.FirstOrDefault(s => s.Name == "Basic Attack");
                    if (basic is not null)
                    {
                        // Prepare runtime globals
                        state = WorldStateOps.WithGlobal(state, g => g with
                        {
                            Vars = g.Vars
                                .SetItem(DslRuntime.CasterKey, pid)
                                .SetItem(DslRuntime.TargetKey, nearestId)
                                .SetItem(DslRuntime.RngKey, rng)
                                .SetItem(DslRuntime.TeamsKey, teamOf)
                        });
                        var cfg2 = new ActionValidationConfig(
                            CasterId: pid,
                            TargetUnitId: nearestId,
                            TeamOfUnit: teamOf,
                            Targeting: TargetingMode.EnemiesOnly,
                            CurrentTurn: state.Global.Turn,
                            CurrentDay: day,
                            CurrentPhase: phase
                        );
                        var validator2 = ActionValidators.ForSkillWithExtras(basic.Compiled, cfg2, cooldowns);
                        var se2 = new SkillExecutor();
                        // attack costs 0.5 MP like client
                        var mpObj = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
                        double mp = mpObj is double dd ? dd : (mpObj is int i2 ? i2 : 0);
                        double cost = GameConstants.BasicAttackCost; if (mp < cost) return;

                        AppendPublic(new[] { $"【{bossName}】攻击 → {nearestId}" });

                        (state, var log) = se2.ExecutePlan(state, basic.Compiled.BuildPlan(new Context(state)), validator2);
                        AppendDebugFor(pid, log.Messages);

                        (state, var log2) = se2.Execute(state, new AtomicAction[] { new ModifyUnitVar(pid, Keys.Mp, v => (v is double d0 ? d0 : Convert.ToDouble(v)) - cost) });
                        AppendDebugFor(pid, log2.Messages);
                        BroadcastBoard(day, phase);
                        cooldowns.SetLastUseTurn(pid, basic.Name, state.Global.Turn);
                        return;
                    }
                }
            }

            // Move towards nearest enemy (one step if possible)
            bool rooted = state.Units[pid].Tags.Contains(Tags.Rooted);
            if (rooted)
                return;
            var speed = GetInt(pid, Keys.Speed, 3);
            var reachable = ReachableCells(pid, speed);
            if (reachable.Count == 0) return;
            var targetPos = ctx.GetUnitVar<Coord>(nearestId, Keys.Pos, default);
            Coord best = default; int bestDist = int.MaxValue;
            foreach (var p in reachable)
            {
                int dd = Math.Abs(p.X - targetPos.X) + Math.Abs(p.Y - targetPos.Y);
                if (dd < bestDist) { bestDist = dd; best = p; }
            }
            if (!best.Equals(default(Coord)))
            {
                var se = new SkillExecutor();
                var mpObj0 = ctx.GetUnitVar<object>(pid, Keys.Mp, 0);
                double mp0 = mpObj0 is double dd0 ? dd0 : (mpObj0 is int ii0 ? ii0 : 0);
                double moveCost = GameConstants.MovementCost; if (mp0 < moveCost) return;

                var curPos = ctx.GetUnitVar<Coord>(pid, Keys.Pos, default);
                AppendPublic(new[] { $"【{bossName}】移动 {curPos} → {best}" });

                (state, var log) = se.Execute(state, new AtomicAction[] { new Move(pid, best), new ModifyUnitVar(pid, Keys.Mp, v => (v is double d ? d : Convert.ToDouble(v)) - moveCost) });
                AppendDebugFor(pid, log.Messages);
                BroadcastBoard(day, phase);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Boss AI turn execution failed for {pid}: {ex.GetType().Name} - {ex.Message}");
        }
    }

    private int GetInt(string id, string key, int def = 0)
    {
        if (!state.Units.TryGetValue(id, out var u)) return def;
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
        int[,] dirs = new int[4, 2] { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        while (q.Count > 0)
        {
            var (cur, d) = q.Dequeue();
            if (d == speed) continue;
            for (int i = 0; i < 4; i++)
            {
                var nx = cur.X + dirs[i, 0];
                var ny = cur.Y + dirs[i, 1];
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

    /// <summary>
    /// Check if HP has crossed any thresholds and trigger corresponding quotes.
    /// Only triggers each threshold once per unit.
    /// </summary>
    private void CheckHpThresholdQuotes(string unitId)
    {
        if (!roleOf.TryGetValue(unitId, out var role)) return;
        if (role.Quotes.OnHpBelow.IsEmpty) return;

        int currentHp = GetInt(unitId, Keys.Hp, 0);
        int maxHp = GetInt(unitId, Keys.MaxHp, 1);
        if (maxHp <= 0) return;

        double currentHpPct = (double)currentHp / maxHp;

        // Get or create triggered set for this unit
        if (!triggeredHpThresholds.TryGetValue(unitId, out var triggered))
        {
            triggered = new HashSet<double>();
            triggeredHpThresholds[unitId] = triggered;
        }

        // Check all thresholds in descending order (highest first)
        var thresholds = role.Quotes.OnHpBelow.Keys.OrderByDescending(t => t).ToList();
        foreach (var threshold in thresholds)
        {
            // If HP is below threshold and we haven't triggered this threshold yet
            if (currentHpPct <= threshold && !triggered.Contains(threshold))
            {
                var quotes = role.Quotes.OnHpBelow[threshold];
                var quote = ETBBS.RoleQuotes.GetRandom(quotes, rng);
                if (!string.IsNullOrEmpty(quote))
                {
                    var unitName = unitId == bossId ? bossName : unitId;
                    AppendPublic(new[] { $"💬 【{unitName}】：\"{quote}\"" });
                }
                // Mark this threshold as triggered
                triggered.Add(threshold);
                // Only trigger one threshold per check
                break;
            }
        }
    }
}
