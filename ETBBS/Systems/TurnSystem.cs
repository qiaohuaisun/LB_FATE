using Microsoft.Extensions.Logging;

namespace ETBBS;

/// <summary>
/// Manages turn-based game flow including status effects, regeneration, and DoT.
/// </summary>
public sealed class TurnSystem
{
    private readonly ILogger<TurnSystem> _logger;

    public TurnSystem()
    {
        _logger = ETBBSLog.CreateLogger<TurnSystem>();
    }

    public (WorldState, ExecutionLog) AdvanceTurn(WorldState state, EventBus? events = null)
    {
        var turnTimer = System.Diagnostics.Stopwatch.StartNew();
        var log = new ExecutionLog(new List<string>());
        var oldTurn = state.Global.Turn;

        _logger.LogInformation("=== Turn {Turn} Start === ({UnitCount} units active)", oldTurn, state.Units.Count);
        events?.Publish(EventTopics.TurnStart, oldTurn);

        var cur = state;
        // increment global turn
        cur = WorldStateOps.WithGlobal(cur, g => g with { Turn = g.Turn + 1 });
        var newTurn = cur.Global.Turn;

        _logger.LogDebug("Turn advanced: {OldTurn} -> {NewTurn}", oldTurn, newTurn);

        // Global toggles that tick down per day: reverse heal / reverse damage
        if (cur.Global.Vars.TryGetValue(Keys.ReverseHealTurnsGlobal, out var rvt))
        {
            var turns = rvt is int i ? i : (rvt is long l ? (int)l : (rvt is double d ? (int)Math.Round(d) : 0));
            if (turns > 0)
            {
                var nt = turns - 1;
                if (nt > 0)
                    cur = WorldStateOps.WithGlobal(cur, g => g with { Vars = g.Vars.SetItem(Keys.ReverseHealTurnsGlobal, nt) });
                else
                    cur = WorldStateOps.WithGlobal(cur, g => g with { Vars = g.Vars.Remove(Keys.ReverseHealTurnsGlobal) });
            }
        }
        if (cur.Global.Vars.TryGetValue(Keys.ReverseDamageTurnsGlobal, out var rvd))
        {
            var turns = rvd is int i ? i : (rvd is long l ? (int)l : (rvd is double d ? (int)Math.Round(d) : 0));
            if (turns > 0)
            {
                var nt = turns - 1;
                if (nt > 0)
                    cur = WorldStateOps.WithGlobal(cur, g => g with { Vars = g.Vars.SetItem(Keys.ReverseDamageTurnsGlobal, nt) });
                else
                    cur = WorldStateOps.WithGlobal(cur, g => g with { Vars = g.Vars.Remove(Keys.ReverseDamageTurnsGlobal) });
            }
        }

        // per-unit maintenance
        foreach (var (id, unit) in state.Units)
        {
            // undying tick
            if (unit.Vars.TryGetValue(Keys.UndyingTurns, out var uv) && uv is int turns && turns > 0)
            {
                var newTurns = turns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.UndyingTurns, newTurns) });
                events?.Publish(EventTopics.UndyingTick, new { unit = id, remaining = newTurns });
                log.Info($"Undying tick for {id}: {turns} -> {newTurns}");
                if (newTurns <= 0)
                {
                    // optional: remove Undying tag if present
                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Tags = u.Tags.Remove(Tags.Undying) });
                    events?.Publish(EventTopics.UndyingEnd, id);
                }
            }

            // status ticks: stunned/silenced/rooted
            if (unit.Vars.TryGetValue(Keys.StunnedTurns, out var stv) && stv is int st && st > 0)
            {
                var ns = st - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.StunnedTurns, ns), Tags = u.Tags.Add(Tags.Stunned) });
                if (ns <= 0)
                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Tags = u.Tags.Remove(Tags.Stunned) });
            }
            // untargetable tick
            if (unit.Vars.TryGetValue(Keys.UntargetableTurns, out var utv) && utv is int utt && utt > 0)
            {
                var ns = utt - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.UntargetableTurns, ns) });
            }
            // cannot act tick
            if (unit.Vars.TryGetValue(Keys.CannotActTurns, out var catv) && catv is int cat && cat > 0)
            {
                var ns = cat - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.CannotActTurns, ns) });
            }
            // on-damage heal duration tick
            if (unit.Vars.TryGetValue(Keys.OnDamageHealTurns, out var odtv) && odtv is int odt && odt > 0)
            {
                var ns = odt - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.OnDamageHealTurns, ns) });
            }
            if (unit.Vars.TryGetValue(Keys.SilencedTurns, out var slv) && slv is int sl && sl > 0)
            {
                var ns = sl - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.SilencedTurns, ns), Tags = u.Tags.Add(Tags.Silenced) });
                if (ns <= 0)
                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Tags = u.Tags.Remove(Tags.Silenced) });
            }
            if (unit.Vars.TryGetValue(Keys.RootedTurns, out var rtv) && rtv is int rt && rt > 0)
            {
                var ns = rt - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.RootedTurns, ns), Tags = u.Tags.Add(Tags.Rooted) });
                if (ns <= 0)
                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Tags = u.Tags.Remove(Tags.Rooted) });
            }
            // status immunity tick
            if (unit.Vars.TryGetValue(Keys.StatusImmuneTurns, out var imv) && imv is int im && im > 0)
            {
                var ns = im - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.StatusImmuneTurns, ns) });
            }

            // no heal tick
            if (unit.Vars.TryGetValue(Keys.NoHealTurns, out var nht) && nht is int nhti && nhti > 0)
            {
                var ns = nhti - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.NoHealTurns, ns) });
            }

            // Timed evasion bonus tick
            if (unit.Vars.TryGetValue(Keys.TempEvasionBonusTurns, out var ebv) && ebv is int ebt && ebt > 0)
            {
                var ns = ebt - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.TempEvasionBonusTurns, ns) });
            }
            // Force ignore defense tick
            if (unit.Vars.TryGetValue(Keys.ForceIgnoreDefTurns, out var fiv) && fiv is int fit && fit > 0)
            {
                var ns = fit - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.ForceIgnoreDefTurns, ns) });
            }

            // mp regen
            if (unit.Vars.TryGetValue(Keys.MpRegenPerTurn, out var rv))
            {
                double regen = rv switch { int i => i, double d => d, float f => f, long l => l, _ => 0.0 };
                if (regen != 0.0)
                {
                    object? before = null;
                    if (unit.Vars.TryGetValue(Keys.Mp, out var mv)) before = mv;

                    object after;
                    if (before is int bi)
                        after = bi + (int)Math.Round(regen);
                    else if (before is double bd)
                        after = bd + regen;
                    else if (before is float bf)
                        after = bf + (float)regen;
                    else if (before is long bl)
                        after = bl + (long)Math.Round(regen);
                    else
                        after = regen;

                    // clamp to max_mp if present
                    if (unit.Vars.TryGetValue(Keys.MaxMp, out var mmv))
                    {
                        double maxmp = mmv is double mmd ? mmd : (mmv is int mmi ? mmi : (mmv is long mml ? (double)mml : 0));
                        if (after is int ai) after = Math.Min(ai, (int)Math.Round(maxmp));
                        else if (after is long al) after = Math.Min(al, (long)Math.Round(maxmp));
                        else if (after is double ad) after = Math.Min(ad, maxmp);
                        else if (after is float af) after = Math.Min(af, (float)maxmp);
                    }

                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.Mp, after) });
                    events?.Publish(EventTopics.UnitMpRegen, new { unit = id, before, delta = regen, after });
                    log.Info($"MP regen for {id}: +{regen}");
                }
            }

            // hp regen (new)
            if (unit.Vars.TryGetValue(Keys.HpRegenPerTurn, out var hrv))
            {
                double hregen = hrv switch { int i => i, double d => d, float f => f, long l => l, _ => 0.0 };
                if (hregen != 0.0)
                {
                    cur = WorldStateOps.WithUnit(cur, id, u =>
                    {
                        var hp = 0;
                        if (u.Vars.TryGetValue(Keys.Hp, out var hv))
                            hp = hv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
                        int maxHp = u.Vars.TryGetValue(Keys.MaxHp, out var mhv) ? (mhv is int mi ? mi : (mhv is long ml ? (int)ml : (mhv is double md ? (int)Math.Round(md) : 0))) : int.MaxValue;
                        var nhp = Math.Min(maxHp, hp + (int)Math.Round(hregen));
                        return u with { Vars = u.Vars.SetItem(Keys.Hp, nhp) };
                    });
                    log.Info($"HP regen for {id}: +{hregen}");
                }
            }

            // magic resist per-turn increment is handled via generic per_turn_add:resist_magic

            // generic per-turn variable increments: per_turn_add:<key> = <double>
            foreach (var kv in unit.Vars)
            {
                const string Prefix = "per_turn_add:";
                if (kv.Key.StartsWith(Prefix))
                {
                    var targetKey = kv.Key.Substring(Prefix.Length);
                    if (string.IsNullOrWhiteSpace(targetKey)) continue;
                    double delta = kv.Value switch { int i => i, long l => l, double d => d, float f => f, _ => 0.0 };
                    if (delta == 0.0) continue;
                    cur = WorldStateOps.WithUnit(cur, id, u =>
                    {
                        double curVal = 0.0; object? oldObj = null;
                        if (u.Vars.TryGetValue(targetKey, out var v)) { oldObj = v; curVal = v switch { int i => i, long l => l, double d => d, float f => f, _ => 0.0 }; }
                        double newVal = curVal + delta;
                        // clamp heuristics
                        // generic max from per_turn_max:<key> or max_<key>
                        if (u.Vars.TryGetValue($"per_turn_max:{targetKey}", out var mx) || u.Vars.TryGetValue($"max_{targetKey}", out mx))
                        {
                            var maxd = mx switch { int i => (double)i, long l => (double)l, double d => d, float f => (double)f, _ => (double?)null };
                            if (maxd is double mm) newVal = Math.Min(newVal, mm);
                        }
                        // conventional caps
                        if (targetKey == Keys.Hp && u.Vars.TryGetValue(Keys.MaxHp, out var mh))
                        {
                            var mm = mh switch { int i => (double)i, long l => (double)l, double d => d, float f => (double)f, _ => double.PositiveInfinity };
                            newVal = Math.Min(newVal, mm);
                        }
                        if (targetKey == Keys.Mp && u.Vars.TryGetValue(Keys.MaxMp, out var mmv2))
                        {
                            var mm = mmv2 switch { int i => (double)i, long l => (double)l, double d => d, float f => (double)f, _ => double.PositiveInfinity };
                            newVal = Math.Min(newVal, mm);
                        }
                        if (targetKey.StartsWith("resist_"))
                        {
                            newVal = Math.Clamp(newVal, 0.0, 1.0);
                        }
                        object outVal;
                        if (oldObj is int or long || targetKey == Keys.Hp)
                            outVal = (int)Math.Round(newVal);
                        else if (oldObj is float)
                            outVal = (float)newVal;
                        else
                            outVal = newVal;
                        return u with { Vars = u.Vars.SetItem(targetKey, outVal) };
                    });
                }
            }

            // bleed/burn damage over time per day (tick once per AdvanceTurn)
            if (unit.Vars.TryGetValue(Keys.BleedTurns, out var btv) && btv is int bt && bt > 0)
            {
                var dpt = 1;
                if (unit.Vars.TryGetValue(Keys.BleedPerTurn, out var bpv))
                    dpt = bpv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 1 };
                cur = WorldStateOps.WithUnit(cur, id, u =>
                {
                    var hp = 0;
                    if (u.Vars.TryGetValue(Keys.Hp, out var hv))
                        hp = hv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
                    var nhp = Math.Max(0, hp - Math.Max(0, dpt));
                    var nv = u.Vars.SetItem(Keys.BleedTurns, bt - 1).SetItem(Keys.Hp, nhp);
                    if (bt - 1 <= 0) nv = nv.Remove(Keys.BleedPerTurn);
                    return u with { Vars = nv, Tags = (bt - 1 <= 0) ? u.Tags.Remove(Tags.Bleeding) : u.Tags };
                });
                events?.Publish(EventTopics.ActionExecuted, new ActionExecutedEvent(state, cur, new Damage(id, dpt)));
                log.Info($"Bleed tick on {id}: -{dpt}");
            }

            if (unit.Vars.TryGetValue(Keys.BurnTurns, out var ftv) && ftv is int ft && ft > 0)
            {
                var dpt = 1;
                if (unit.Vars.TryGetValue(Keys.BurnPerTurn, out var fpv))
                    dpt = fpv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 1 };
                cur = WorldStateOps.WithUnit(cur, id, u =>
                {
                    var hp = 0;
                    if (u.Vars.TryGetValue(Keys.Hp, out var hv))
                        hp = hv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
                    var nhp = Math.Max(0, hp - Math.Max(0, dpt));
                    var nv = u.Vars.SetItem(Keys.BurnTurns, ft - 1).SetItem(Keys.Hp, nhp);
                    if (ft - 1 <= 0) nv = nv.Remove(Keys.BurnPerTurn);
                    return u with { Vars = nv, Tags = (ft - 1 <= 0) ? u.Tags.Remove(Tags.Burning) : u.Tags };
                });
                events?.Publish(EventTopics.ActionExecuted, new ActionExecutedEvent(state, cur, new Damage(id, dpt)));
                log.Info($"Burn tick on {id}: -{dpt}");
            }
        }

        turnTimer.Stop();

        // Summary logging
        int statusEffectsActive = 0;
        int unitsWithDot = 0;
        int unitsWithRegen = 0;

        foreach (var (id, unit) in cur.Units)
        {
            if (unit.Vars.ContainsKey(Keys.StunnedTurns) || unit.Vars.ContainsKey(Keys.SilencedTurns) || unit.Vars.ContainsKey(Keys.RootedTurns))
                statusEffectsActive++;
            if (unit.Vars.ContainsKey(Keys.BleedTurns) || unit.Vars.ContainsKey(Keys.BurnTurns))
                unitsWithDot++;
            if (unit.Vars.ContainsKey(Keys.HpRegenPerTurn) || unit.Vars.ContainsKey(Keys.MpRegenPerTurn))
                unitsWithRegen++;
        }

        _logger.LogInformation("=== Turn {Turn} End === Duration: {ElapsedMs}ms, Status: {StatusCount}, DoT: {DotCount}, Regen: {RegenCount}",
            newTurn, turnTimer.Elapsed.TotalMilliseconds, statusEffectsActive, unitsWithDot, unitsWithRegen);

        events?.Publish(EventTopics.TurnEnd, cur.Global.Turn);
        return (cur, log);
    }
}


