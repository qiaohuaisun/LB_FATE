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
        int reverseHealTurns = TypeConversion.GetIntFrom(cur.Global.Vars, Keys.ReverseHealTurnsGlobal);
        if (reverseHealTurns > 0)
        {
            var newTurns = reverseHealTurns - 1;
            if (newTurns > 0)
                cur = WorldStateOps.WithGlobal(cur, g => g with { Vars = g.Vars.SetItem(Keys.ReverseHealTurnsGlobal, newTurns) });
            else
                cur = WorldStateOps.WithGlobal(cur, g => g with { Vars = g.Vars.Remove(Keys.ReverseHealTurnsGlobal) });
        }

        int reverseDamageTurns = TypeConversion.GetIntFrom(cur.Global.Vars, Keys.ReverseDamageTurnsGlobal);
        if (reverseDamageTurns > 0)
        {
            var newTurns = reverseDamageTurns - 1;
            if (newTurns > 0)
                cur = WorldStateOps.WithGlobal(cur, g => g with { Vars = g.Vars.SetItem(Keys.ReverseDamageTurnsGlobal, newTurns) });
            else
                cur = WorldStateOps.WithGlobal(cur, g => g with { Vars = g.Vars.Remove(Keys.ReverseDamageTurnsGlobal) });
        }

        // per-unit maintenance
        foreach (var (id, unit) in state.Units)
        {
            // undying tick
            int undyingTurns = unit.GetIntVar(Keys.UndyingTurns);
            if (undyingTurns > 0)
            {
                var newTurns = undyingTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.UndyingTurns, newTurns) });
                events?.Publish(EventTopics.UndyingTick, new { unit = id, remaining = newTurns });
                log.Info($"Undying tick for {id}: {undyingTurns} -> {newTurns}");
                if (newTurns <= 0)
                {
                    // optional: remove Undying tag if present
                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Tags = u.Tags.Remove(Tags.Undying) });
                    events?.Publish(EventTopics.UndyingEnd, id);
                }
            }

            // status ticks: stunned/silenced/rooted
            int stunnedTurns = unit.GetIntVar(Keys.StunnedTurns);
            if (stunnedTurns > 0)
            {
                var newTurns = stunnedTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.StunnedTurns, newTurns), Tags = u.Tags.Add(Tags.Stunned) });
                if (newTurns <= 0)
                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Tags = u.Tags.Remove(Tags.Stunned) });
            }

            // untargetable tick
            int untargetableTurns = unit.GetIntVar(Keys.UntargetableTurns);
            if (untargetableTurns > 0)
            {
                var newTurns = untargetableTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.UntargetableTurns, newTurns) });
            }

            // cannot act tick
            int cannotActTurns = unit.GetIntVar(Keys.CannotActTurns);
            if (cannotActTurns > 0)
            {
                var newTurns = cannotActTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.CannotActTurns, newTurns) });
            }

            // on-damage heal duration tick
            int onDamageHealTurns = unit.GetIntVar(Keys.OnDamageHealTurns);
            if (onDamageHealTurns > 0)
            {
                var newTurns = onDamageHealTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.OnDamageHealTurns, newTurns) });
            }

            int silencedTurns = unit.GetIntVar(Keys.SilencedTurns);
            if (silencedTurns > 0)
            {
                var newTurns = silencedTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.SilencedTurns, newTurns), Tags = u.Tags.Add(Tags.Silenced) });
                if (newTurns <= 0)
                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Tags = u.Tags.Remove(Tags.Silenced) });
            }

            int rootedTurns = unit.GetIntVar(Keys.RootedTurns);
            if (rootedTurns > 0)
            {
                var newTurns = rootedTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.RootedTurns, newTurns), Tags = u.Tags.Add(Tags.Rooted) });
                if (newTurns <= 0)
                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Tags = u.Tags.Remove(Tags.Rooted) });
            }

            // status immunity tick
            int statusImmuneTurns = unit.GetIntVar(Keys.StatusImmuneTurns);
            if (statusImmuneTurns > 0)
            {
                var newTurns = statusImmuneTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.StatusImmuneTurns, newTurns) });
            }

            // no heal tick
            int noHealTurns = unit.GetIntVar(Keys.NoHealTurns);
            if (noHealTurns > 0)
            {
                var newTurns = noHealTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.NoHealTurns, newTurns) });
            }

            // Timed evasion bonus tick
            int evasionBonusTurns = unit.GetIntVar(Keys.TempEvasionBonusTurns);
            if (evasionBonusTurns > 0)
            {
                var newTurns = evasionBonusTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.TempEvasionBonusTurns, newTurns) });
            }

            // Force ignore defense tick
            int ignoreDefTurns = unit.GetIntVar(Keys.ForceIgnoreDefTurns);
            if (ignoreDefTurns > 0)
            {
                var newTurns = ignoreDefTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.ForceIgnoreDefTurns, newTurns) });
            }

            // Damage reduction tick
            int damageReductionTurns = unit.GetIntVar(Keys.DamageReductionTurns);
            if (damageReductionTurns > 0)
            {
                var newTurns = damageReductionTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.DamageReductionTurns, newTurns) });
            }

            // Slow debuff tick (restores original speed when expires)
            int slowTurns = unit.GetIntVar(Keys.SlowTurns);
            if (slowTurns > 0)
            {
                var newTurns = slowTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.SlowTurns, newTurns) });
                if (newTurns <= 0)
                {
                    // Restore original speed if stored
                    cur = WorldStateOps.WithUnit(cur, id, u =>
                    {
                        if (u.Vars.TryGetValue(Keys.OriginalSpeed, out var origSpeed))
                            return u with { Vars = u.Vars.SetItem(Keys.Speed, origSpeed).Remove(Keys.OriginalSpeed).Remove(Keys.SlowTurns) };
                        return u with { Vars = u.Vars.Remove(Keys.SlowTurns) };
                    });
                    log.Info($"Slow debuff expired for {id}, speed restored");
                }
            }

            // Weakened attack tick (restores original attack when expires)
            int weakenedAttackTurns = unit.GetIntVar(Keys.WeakenedAttackTurns);
            if (weakenedAttackTurns > 0)
            {
                var newTurns = weakenedAttackTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.WeakenedAttackTurns, newTurns) });
                if (newTurns <= 0)
                {
                    // Restore attack by adding back the debuff amount
                    cur = WorldStateOps.WithUnit(cur, id, u =>
                    {
                        int debuffAmount = u.GetIntVar(Keys.AttackDebuffAmount, 0);
                        int currentAtk = u.GetIntVar(Keys.Atk, 0);
                        return u with { Vars = u.Vars.SetItem(Keys.Atk, currentAtk + debuffAmount).Remove(Keys.WeakenedAttackTurns).Remove(Keys.AttackDebuffAmount) };
                    });
                    log.Info($"Weakened attack expired for {id}, attack restored");
                }
            }

            // Weakened magic attack tick (restores original magic attack when expires)
            int weakenedMagicAttackTurns = unit.GetIntVar(Keys.WeakenedMagicAttackTurns);
            if (weakenedMagicAttackTurns > 0)
            {
                var newTurns = weakenedMagicAttackTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.WeakenedMagicAttackTurns, newTurns) });
                if (newTurns <= 0)
                {
                    // Restore magic attack by adding back the debuff amount
                    cur = WorldStateOps.WithUnit(cur, id, u =>
                    {
                        int debuffAmount = u.GetIntVar(Keys.MagicAttackDebuffAmount, 0);
                        int currentMAtk = u.GetIntVar(Keys.MAtk, 0);
                        return u with { Vars = u.Vars.SetItem(Keys.MAtk, currentMAtk + debuffAmount).Remove(Keys.WeakenedMagicAttackTurns).Remove(Keys.MagicAttackDebuffAmount) };
                    });
                    log.Info($"Weakened magic attack expired for {id}, magic attack restored");
                }
            }

            // Intimidated debuff tick (affects both attack and magic attack)
            int intimidatedTurns = unit.GetIntVar(Keys.IntimidatedTurns);
            if (intimidatedTurns > 0)
            {
                var newTurns = intimidatedTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.IntimidatedTurns, newTurns) });
                log.Info($"Intimidated tick for {id}: {intimidatedTurns} -> {newTurns}");
            }

            // Weakened defense tick (restores original defense when expires)
            int weakenedDefenseTurns = unit.GetIntVar(Keys.WeakenedDefenseTurns);
            if (weakenedDefenseTurns > 0)
            {
                var newTurns = weakenedDefenseTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.WeakenedDefenseTurns, newTurns) });
                if (newTurns <= 0)
                {
                    // Restore defense by adding back the debuff amount
                    cur = WorldStateOps.WithUnit(cur, id, u =>
                    {
                        int debuffAmount = u.GetIntVar(Keys.DefenseDebuffAmount, 0);
                        int currentDef = u.GetIntVar(Keys.Def, 0);
                        return u with { Vars = u.Vars.SetItem(Keys.Def, currentDef + debuffAmount).Remove(Keys.WeakenedDefenseTurns).Remove(Keys.DefenseDebuffAmount) };
                    });
                    log.Info($"Weakened defense expired for {id}, defense restored");
                }
            }

            // Thorn shield duration tick (damage reflection handled in DamageCalculation)
            int thornShieldTurns = unit.GetIntVar(Keys.ThornShieldTurns);
            if (thornShieldTurns > 0)
            {
                var newTurns = thornShieldTurns - 1;
                cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.SetItem(Keys.ThornShieldTurns, newTurns) });
                if (newTurns <= 0)
                {
                    cur = WorldStateOps.WithUnit(cur, id, u => u with { Vars = u.Vars.Remove(Keys.ThornShieldTurns).Remove(Keys.ThornDamage) });
                    log.Info($"Thorn shield expired for {id}");
                }
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
            double hpRegen = unit.GetDoubleVar(Keys.HpRegenPerTurn);
            if (hpRegen != 0.0)
            {
                cur = WorldStateOps.WithUnit(cur, id, u =>
                {
                    int hp = u.GetIntVar(Keys.Hp);
                    // Dead units (HP <= 0) cannot regenerate HP - prevents "resurrection"
                    if (hp <= 0)
                        return u;

                    int maxHp = u.GetIntVar(Keys.MaxHp, int.MaxValue);
                    int newHp = Math.Min(maxHp, hp + (int)Math.Round(hpRegen));
                    return u with { Vars = u.Vars.SetItem(Keys.Hp, newHp) };
                });
                log.Info($"HP regen for {id}: +{hpRegen}");
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
                            newVal = Math.Clamp(newVal, GameConstants.MinResistanceCap, GameConstants.MaxResistanceCap);
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
            int bleedTurns = unit.GetIntVar(Keys.BleedTurns);
            if (bleedTurns > 0)
            {
                int damagePerTurn = unit.GetIntVar(Keys.BleedPerTurn, GameConstants.DefaultBleedDamagePerTurn);
                cur = WorldStateOps.WithUnit(cur, id, u =>
                {
                    int hp = u.GetIntVar(Keys.Hp);
                    int newHp = Math.Max(0, hp - Math.Max(0, damagePerTurn));
                    var newVars = u.Vars.SetItem(Keys.BleedTurns, bleedTurns - 1).SetItem(Keys.Hp, newHp);
                    if (bleedTurns - 1 <= 0) newVars = newVars.Remove(Keys.BleedPerTurn);
                    return u with { Vars = newVars, Tags = (bleedTurns - 1 <= 0) ? u.Tags.Remove(Tags.Bleeding) : u.Tags };
                });
                events?.Publish(EventTopics.ActionExecuted, new ActionExecutedEvent(state, cur, new Damage(id, damagePerTurn)));
                log.Info($"Bleed tick on {id}: -{damagePerTurn}");
            }

            int burnTurns = unit.GetIntVar(Keys.BurnTurns);
            if (burnTurns > 0)
            {
                int damagePerTurn = unit.GetIntVar(Keys.BurnPerTurn, GameConstants.DefaultBurnDamagePerTurn);
                cur = WorldStateOps.WithUnit(cur, id, u =>
                {
                    int hp = u.GetIntVar(Keys.Hp);
                    int newHp = Math.Max(0, hp - Math.Max(0, damagePerTurn));
                    var newVars = u.Vars.SetItem(Keys.BurnTurns, burnTurns - 1).SetItem(Keys.Hp, newHp);
                    if (burnTurns - 1 <= 0) newVars = newVars.Remove(Keys.BurnPerTurn);
                    return u with { Vars = newVars, Tags = (burnTurns - 1 <= 0) ? u.Tags.Remove(Tags.Burning) : u.Tags };
                });
                events?.Publish(EventTopics.ActionExecuted, new ActionExecutedEvent(state, cur, new Damage(id, damagePerTurn)));
                log.Info($"Burn tick on {id}: -{damagePerTurn}");
            }

            // Generic *_turns countdown: handle any custom *_turns variables not explicitly handled above
            // This allows role designers to create custom timed effects without modifying TurnSystem
            var knownTurnsVars = new HashSet<string>
            {
                Keys.UndyingTurns, Keys.StunnedTurns, Keys.SilencedTurns, Keys.RootedTurns,
                Keys.StatusImmuneTurns, Keys.NoHealTurns, Keys.TempEvasionBonusTurns,
                Keys.ForceIgnoreDefTurns, Keys.DamageReductionTurns, Keys.SlowTurns,
                Keys.WeakenedAttackTurns, Keys.WeakenedMagicAttackTurns, Keys.WeakenedDefenseTurns,
                Keys.IntimidatedTurns, Keys.ThornShieldTurns, Keys.OnDamageHealTurns,
                Keys.UntargetableTurns, Keys.CannotActTurns, Keys.BleedTurns, Keys.BurnTurns
            };

            var customTurnsVars = unit.Vars
                .Where(kv => kv.Key.EndsWith("_turns") && !knownTurnsVars.Contains(kv.Key))
                .ToList();

            foreach (var (key, value) in customTurnsVars)
            {
                int turns = value switch
                {
                    int i => i,
                    long l => (int)l,
                    double d => (int)Math.Round(d),
                    _ => 0
                };

                if (turns > 0)
                {
                    int newTurns = turns - 1;
                    cur = WorldStateOps.WithUnit(cur, id, u =>
                    {
                        if (newTurns <= 0)
                        {
                            // Remove the *_turns variable when it reaches 0
                            // Also remove corresponding tag if it exists (e.g., exposed_turns -> exposed)
                            var tagName = key.Replace("_turns", "");
                            return u with
                            {
                                Vars = u.Vars.Remove(key),
                                Tags = u.Tags.Contains(tagName) ? u.Tags.Remove(tagName) : u.Tags
                            };
                        }
                        else
                        {
                            return u with { Vars = u.Vars.SetItem(key, newTurns) };
                        }
                    });
                }
            }
        }

        turnTimer.Stop();

        // Remove dead units (HP <= 0)
        var deadUnits = new List<string>();
        foreach (var (id, unit) in cur.Units)
        {
            // If HP is not present, do not treat as dead — some tests/units omit HP intentionally
            if (!unit.Vars.TryGetValue(Keys.Hp, out var hpVal))
                continue;

            int hp = hpVal switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)Math.Round(d),
                _ => 0
            };
            if (hp <= 0) deadUnits.Add(id);
        }

        foreach (var deadId in deadUnits)
        {
            _logger.LogInformation("Unit died and removed: {UnitId}", deadId);
            cur = cur with { Units = cur.Units.Remove(deadId) };
            events?.Publish(EventTopics.UnitDied, new UnitDiedEvent(deadId));
            log.Info($"Unit {deadId} died and was removed from the game.");
        }

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

