namespace ETBBS;

/// <summary>
/// Centralized damage calculation and HP modification logic.
/// Extracts common patterns from Damage, PhysicalDamage, and MagicDamage actions.
/// </summary>
public static class DamageCalculation
{
    /// <summary>
    /// Result of applying damage with shields, heals, and undying mechanics.
    /// </summary>
    public record DamageResult(
        int FinalHp,
        UnitState ModifiedUnit,
        int DamageBlocked,
        bool TriggeredAutoHeal,
        bool TriggeredOnDamageHeal,
        bool PreventedDeathByUndying
    );

    /// <summary>
    /// Applies damage to a unit with full damage pipeline:
    /// 1. Shield absorption
    /// 2. HP reduction
    /// 3. Auto-heal below half (first time trigger)
    /// 4. On-damage heal (if active)
    /// 5. Undying prevention (cannot die while undying)
    /// </summary>
    /// <param name="unit">The unit taking damage</param>
    /// <param name="rawDamage">Raw damage amount before shields</param>
    /// <returns>DamageResult with final HP and modified unit state</returns>
    public static DamageResult ApplyDamage(UnitState unit, int rawDamage)
    {
        int originalHp = unit.GetIntVar(Keys.Hp);
        int damageToApply = Math.Max(0, rawDamage);
        int damageBlocked = 0;
        bool triggeredAutoHeal = false;
        bool triggeredOnDamageHeal = false;
        bool preventedDeath = false;

        var modifiedUnit = unit;

        // Step 1: Shield absorption
        if (modifiedUnit.Vars.TryGetValue(Keys.ShieldValue, out var sv))
        {
            double shield = TypeConversion.ToDouble(sv);
            double afterShield = shield - damageToApply;

            if (afterShield >= 0)
            {
                // Shield fully absorbed the damage
                damageBlocked = damageToApply;
                modifiedUnit = modifiedUnit with { Vars = modifiedUnit.Vars.SetItem(Keys.ShieldValue, afterShield) };
                return new DamageResult(originalHp, modifiedUnit, damageBlocked, false, false, false);
            }
            else
            {
                // Shield partially absorbed, damage breaks through
                damageBlocked = (int)Math.Round(shield);
                damageToApply = (int)Math.Max(0, Math.Round(-afterShield));
                modifiedUnit = modifiedUnit with { Vars = modifiedUnit.Vars.SetItem(Keys.ShieldValue, 0) };
            }
        }

        // Step 2: Apply damage to HP
        int newHp = Math.Max(0, originalHp - damageToApply);

        // Step 3: Auto-heal below half (first time)
        int maxHp = modifiedUnit.GetIntVar(Keys.MaxHp);
        bool alreadyUsed = modifiedUnit.GetBoolVar(Keys.AutoHealBelowHalfUsed, false);

        if (modifiedUnit.Vars.TryGetValue(Keys.AutoHealBelowHalf, out var ahv)
            && !alreadyUsed
            && maxHp > 0
            && originalHp > maxHp / 2
            && newHp <= maxHp / 2)
        {
            int heal = TypeConversion.ToInt(ahv);
            newHp = Math.Min(maxHp, newHp + Math.Max(0, heal));
            modifiedUnit = modifiedUnit with { Vars = modifiedUnit.Vars.SetItem(Keys.AutoHealBelowHalfUsed, true) };
            triggeredAutoHeal = true;
        }

        // Step 4: On-damage heal (active buff)
        int onDamageHealTurns = modifiedUnit.GetIntVar(Keys.OnDamageHealTurns);
        if (onDamageHealTurns > 0)
        {
            int heal = modifiedUnit.GetIntVar(Keys.OnDamageHealValue);
            if (heal > 0)
            {
                int effectiveMaxHp = maxHp > 0 ? maxHp : int.MaxValue;
                newHp = Math.Min(effectiveMaxHp, newHp + heal);
                triggeredOnDamageHeal = true;
            }
        }

        // Step 5: Undying prevents death
        int undyingTurns = modifiedUnit.GetIntVar(Keys.UndyingTurns);
        if (undyingTurns > 0 && newHp <= 0)
        {
            newHp = 1;
            preventedDeath = true;
        }

        // Apply final HP
        modifiedUnit = modifiedUnit with { Vars = modifiedUnit.Vars.SetItem(Keys.Hp, newHp) };

        return new DamageResult(
            FinalHp: newHp,
            ModifiedUnit: modifiedUnit,
            DamageBlocked: damageBlocked,
            TriggeredAutoHeal: triggeredAutoHeal,
            TriggeredOnDamageHeal: triggeredOnDamageHeal,
            PreventedDeathByUndying: preventedDeath
        );
    }

    /// <summary>
    /// Simplified shield-only damage absorption for actions that only need shield logic.
    /// </summary>
    /// <param name="unit">The unit with potential shield</param>
    /// <param name="damage">Damage to absorb</param>
    /// <returns>(remainingDamage, modifiedUnit)</returns>
    public static (int RemainingDamage, UnitState ModifiedUnit) AbsorbWithShield(UnitState unit, int damage)
    {
        if (!unit.Vars.TryGetValue(Keys.ShieldValue, out var sv))
            return (damage, unit);

        double shield = TypeConversion.ToDouble(sv);
        double afterShield = shield - damage;

        if (afterShield >= 0)
        {
            // Shield fully absorbs
            var newUnit = unit with { Vars = unit.Vars.SetItem(Keys.ShieldValue, afterShield) };
            return (0, newUnit);
        }
        else
        {
            // Shield breaks
            int remaining = (int)Math.Max(0, Math.Round(-afterShield));
            var newUnit = unit with { Vars = unit.Vars.SetItem(Keys.ShieldValue, 0) };
            return (remaining, newUnit);
        }
    }
}