using ETBBS;

namespace LB_FATE;

partial class Game
{
    public void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        InitWorld();
        SetupEventLogging();
        var roleCount = registry.All().Count();
        var unitCount = state.Units.Count;
        var initMsg = $"[Init] Roles loaded: {roleCount}, Units created: {unitCount}";
        Console.WriteLine(initMsg);
        AppendPublic(new[] { initMsg });
        if (endpoints.Count > 0)
        {
            foreach (var ep in endpoints.Values) ep.SendLine(initMsg);
        }
        int day = 1;
        lastDay = day; lastPhase = 1;
        while (Alive().Count > 1)
        {
            for (int phase = 1; phase <= 5; phase++)
            {
                lastDay = day; lastPhase = phase;
                ServerLog($"-- Day {day} Phase {phase} --");
                if (phase == 1) { RefillAllMpToMax(); AppendPublic(new[] { "MP 已回满" }); }
                ShowBoard(day, phase);
                BroadcastBoard(day, phase);
                var order = Alive().OrderBy(_ => rng.Next()).ToList();
                foreach (var pid in order)
                {
                    ServerLog($"Turn: {pid}");
                    Turn(pid, phase, day);
                    // Process force-act request once per loop (if any)
                    if ((state.Global.Vars.TryGetValue("force_act_now", out var fan) && fan is bool fb && fb)
                        || state.Global.Tags.Contains("force_act_now"))
                    {
                        var tgtId = state.Global.Vars.TryGetValue(DslRuntime.TargetKey, out var tv) ? tv as string : null;
                        // Clear the trigger to avoid loops
                        state = WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars.Remove("force_act_now"), Tags = g.Tags.Remove("force_act_now") });
                        if (!string.IsNullOrWhiteSpace(tgtId) && state.Units.ContainsKey(tgtId!) && GetInt(tgtId!, Keys.Hp, 0) > 0)
                        {
                            Turn(tgtId!, phase, day);
                        }
                    }
                    if (Alive().Count <= 1) break;
                }
                if (Alive().Count <= 1) break;

                // Phase-based ticking: apply per-phase status/DoT/regen and advance global turn counter
                var tsPhase = new TurnSystem();
                (state, _) = tsPhase.AdvanceTurn(state, events);

                // Surgery interrupt: if boss took enough damage this phase, release 'stasis' from allies
                try
                {
                    int threshold = 0;
                    // read threshold from global, else from boss's max_hp heuristic (20% of max, capped)
                    if (state.Global.Vars.TryGetValue("surgery_interrupt_ratio", out var tr) && (tr is double dr || tr is int ir))
                    {
                        double ratio = tr is double ddr ? ddr : (tr is int iir ? iir : 0);
                        ratio = Math.Clamp(ratio, 0.05, 1.0);
                        if (state.Units.ContainsKey(bossId))
                        {
                            int maxHp = GetInt(bossId, Keys.MaxHp, 100);
                            threshold = Math.Max(1, (int)Math.Round(maxHp * ratio));
                        }
                    }
                    else if (state.Global.Vars.TryGetValue("surgery_interrupt_threshold", out var tv) && tv is int ti) threshold = ti;
                    else if (state.Units.ContainsKey(bossId))
                    {
                        int maxHp = GetInt(bossId, Keys.MaxHp, 100);
                        threshold = Math.Max(8, (int)Math.Round(maxHp * 0.2));
                    }
                    if (threshold > 0 && phaseDamageTo.TryGetValue(bossId, out var dealt) && dealt >= threshold)
                    {
                        var toRelease = state.Units.Where(kv => kv.Value.Tags.Contains("stasis")).Select(kv => kv.Key).ToList();
                        foreach (var uid in toRelease)
                        {
                            state = WorldStateOps.WithUnit(state, uid, u => u with { Tags = u.Tags.Remove("stasis"), Vars = u.Vars.SetItem(Keys.CannotActTurns, 0).SetItem(Keys.UntargetableTurns, 0) });
                        }
                        AppendPublic(new[] { $"[手术中断] 对 Boss 的伤害达到阈值，已释放 {toRelease.Count} 名受术者。" });
                    }
                }
                catch { }
                finally { try { phaseDamageTo.Clear(); } catch { } }
            }
            // If game ended during phases, do not advance a new day
            if (Alive().Count <= 1)
                break;

            // Move to next day (no additional per-day ticking; already applied per phase)
            day++;
        }
        var aliveNow = Alive();
        var winner = aliveNow.Count == 1 ? aliveNow[0] : aliveNow.FirstOrDefault();
        Console.WriteLine();
        string endMsg;
        if (!string.IsNullOrEmpty(winner))
        {
            endMsg = $"Winner: {winner} ({classOf[winner]})";
            Console.WriteLine(endMsg);
        }
        else
        {
            endMsg = "No winner.";
            Console.WriteLine(endMsg);
        }
        AppendPublic(new[] { endMsg });
        // Final broadcast so clients see the result
        BroadcastBoard(day, phase: 5);
        if (endpoints.Count > 0)
        {
            foreach (var ep in endpoints.Values) ep.SendLine("GAME OVER");
            foreach (var ep in endpoints.Values) ep.SendLine(endMsg);
        }

        // Show a simple GAME OVER screen/animation locally and notify remote clients with a banner
        ShowGameOverScreen(endMsg);
    }
}

partial class Game
{
    private void ShowGameOverScreen(string endMsg)
    {
        try
        {
            var lines = new List<string>
            {
                "+----------------------------------+",
                "|                                  |",
                "|            GAME  OVER            |",
                "|                                  |",
                "+----------------------------------+"
            };
            if (!string.IsNullOrWhiteSpace(endMsg))
                lines.Insert(3, $"|  {endMsg.PadRight(30).Substring(0,30)}  |");

            // Local console: brief flash animation
            if (endpoints.Count == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    Console.ForegroundColor = (i % 2 == 0) ? ConsoleColor.Red : ConsoleColor.Yellow;
                    Console.WriteLine();
                    foreach (var l in lines) Console.WriteLine(l);
                    Console.ResetColor();
                    Thread.Sleep(400);
                }
            }

            // Remote endpoints: send a clear banner
            if (endpoints.Count > 0)
            {
                foreach (var ep in endpoints.Values)
                {
                    try
                    {
                        ep.SendLine("==================================");
                        ep.SendLine("            GAME  OVER            ");
                        ep.SendLine(endMsg);
                        ep.SendLine("==================================");
                    }
                    catch { }
                }
            }
        }
        catch { }
    }
}
