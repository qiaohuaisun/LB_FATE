namespace ETBBS;

/// <summary>
/// Centralized evasion and attack multiplier calculation logic.
/// Extracts common patterns from PhysicalDamage and MagicDamage actions.
/// </summary>
public static class EvasionCalculation
{
    /// <summary>
    /// Result of evasion check with state modifications.
    /// </summary>
    public record EvasionCheckResult(
        bool Evaded,
        WorldState ModifiedState,
        double AttackMultiplier
    );

    /// <summary>
    /// Performs complete evasion check with guaranteed charges, phase bonuses, and RNG.
    /// If evaded via guaranteed charges, grants counter-attack multiplier to defender.
    /// </summary>
    /// <param name="state">Current world state</param>
    /// <param name="attackerId">Attacker unit ID</param>
    /// <param name="targetId">Target unit ID</param>
    /// <returns>EvasionCheckResult with evaded flag, modified state, and attack multiplier</returns>
    public static EvasionCheckResult CheckEvasion(WorldState state, string attackerId, string targetId)
    {
        var target = state.GetUnitOrNull(targetId);
        if (target is null)
            return new EvasionCheckResult(false, state, 1.0);

        // Step 1: Check guaranteed evasion charges
        int evadeCharges = target.GetIntVar(Keys.EvadeCharges);
        if (evadeCharges > 0)
        {
            // Consume charge and grant counter-attack multiplier
            var modifiedState = WorldStateOps.WithUnit(state, targetId, u => u with
            {
                Vars = u.Vars.SetItem(Keys.EvadeCharges, evadeCharges - 1)
                              .SetItem(Keys.NextAttackMultiplier, GameConstants.EvadeCounterMultiplier)
            });
            return new EvasionCheckResult(true, modifiedState, 1.0);
        }

        // Step 2: Calculate total evasion rate
        double evasion = CalculateEvasionRate(state, target);

        // Step 3: RNG check
        System.Random rng = GetOrCreateRng(state, attackerId, targetId);
        bool evaded = rng.NextDouble() < evasion;

        if (evaded)
        {
            return new EvasionCheckResult(true, state, 1.0);
        }

        // Step 4: Check and consume attacker's NextAttackMultiplier
        var attacker = state.GetUnitOrNull(attackerId);
        double attackMultiplier = 1.0;
        var resultState = state;

        if (attacker is not null && attacker.Vars.TryGetValue(Keys.NextAttackMultiplier, out var nm))
        {
            attackMultiplier = Math.Max(1.0, TypeConversion.ToDouble(nm));
            resultState = WorldStateOps.WithUnit(resultState, attackerId, u => u with
            {
                Vars = u.Vars.Remove(Keys.NextAttackMultiplier)
            });
        }

        return new EvasionCheckResult(false, resultState, attackMultiplier);
    }

    /// <summary>
    /// Calculates total evasion rate for a unit including all bonuses.
    /// </summary>
    /// <param name="state">Current world state</param>
    /// <param name="unit">Target unit</param>
    /// <returns>Clamped evasion rate (0.0 to MaxEvasionCap)</returns>
    public static double CalculateEvasionRate(WorldState state, UnitState unit)
    {
        double evasion = unit.GetDoubleVar(Keys.Evasion);

        // Phase-based bonus (night/dawn phases: 1 and 5)
        int phase = TypeConversion.GetIntFrom(state.Global.Vars, DslRuntime.PhaseKey);
        if (phase == 1 || phase == 5)
        {
            evasion += unit.GetDoubleVar(Keys.NightOrDawnEvasionBonus);
        }

        // Temporary evasion bonus with duration
        if (unit.GetIntVar(Keys.TempEvasionBonusTurns) > 0)
        {
            evasion += unit.GetDoubleVar(Keys.TempEvasionBonus);
        }

        return Math.Clamp(evasion, 0.0, GameConstants.MaxEvasionCap);
    }

    /// <summary>
    /// Gets or creates a Random instance for combat RNG.
    /// Tries to use global RNG from state, or creates a new seeded instance.
    /// </summary>
    private static System.Random GetOrCreateRng(WorldState state, string attackerId, string targetId)
    {
        if (state.Global.Vars.TryGetValue(DslRuntime.RngKey, out var rv) && rv is System.Random r)
            return r;

        // Create seeded RNG based on tick count and IDs
        int seed = unchecked(Environment.TickCount * 31 + attackerId.GetHashCode() + targetId.GetHashCode());
        return new System.Random(seed);
    }
}