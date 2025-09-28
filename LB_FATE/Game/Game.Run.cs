using ETBBS;

namespace LB_FATE;

partial class Game
{
    public void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        InitWorld();
        var roleCount = registry.All().Count();
        var unitCount = state.Units.Count;
        var initMsg = $"[Init] Roles loaded: {roleCount}, Units created: {unitCount}";
        Console.WriteLine(initMsg);
        AppendLog(new[] { initMsg });
        if (endpoints.Count > 0)
        {
            foreach (var ep in endpoints.Values) ep.SendLine(initMsg);
        }
        int day = 1;
        while (Alive().Count > 1)
        {
            for (int phase = 1; phase <= 5; phase++)
            {
                if (phase == 1) { RefillAllMpToMax(); AppendLog(new[] { "MP 已回满" }); }
                ShowBoard(day, phase);
                BroadcastBoard(day, phase);
                var order = Alive().OrderBy(_ => rng.Next()).ToList();
                foreach (var pid in order)
                {
                    Turn(pid, phase, day);
                    // Process force-act request once per loop (if any)
                    if (state.Global.Vars.TryGetValue("force_act_now", out var fan) && fan is bool fb && fb)
                    {
                        var tgtId = state.Global.Vars.TryGetValue(DslRuntime.TargetKey, out var tv) ? tv as string : null;
                        // Clear the trigger to avoid loops
                        state = WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars.Remove("force_act_now") });
                        if (!string.IsNullOrWhiteSpace(tgtId) && state.Units.ContainsKey(tgtId!) && GetInt(tgtId!, Keys.Hp, 0) > 0)
                        {
                            Turn(tgtId!, phase, day);
                        }
                    }
                    if (Alive().Count <= 1) break;
                }
                if (Alive().Count <= 1) break;
            }
            // If game ended during phases, do not advance a new day
            if (Alive().Count <= 1)
                break;

            var ts = new TurnSystem();
            (state, _) = ts.AdvanceTurn(state, events);
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
        AppendLog(new[] { endMsg });
        // Final broadcast so clients see the result
        BroadcastBoard(day, phase: 5);
        if (endpoints.Count > 0)
        {
            foreach (var ep in endpoints.Values) ep.SendLine("GAME OVER");
            foreach (var ep in endpoints.Values) ep.SendLine(endMsg);
        }
    }
}
