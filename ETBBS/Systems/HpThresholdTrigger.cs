namespace ETBBS;

/// <summary>
/// Handles HP threshold-based passive skill triggering.
/// When a unit's HP falls below a specified threshold for the first time,
/// a designated skill is automatically triggered.
/// </summary>
public static class HpThresholdTrigger
{
    /// <summary>
    /// Checks if HP threshold trigger conditions are met and returns the skill ID to execute.
    /// Returns null if no trigger should occur.
    /// </summary>
    /// <param name="unit">The unit to check</param>
    /// <param name="previousHp">HP before damage was applied</param>
    /// <param name="currentHp">HP after damage was applied</param>
    /// <returns>Skill ID to trigger, or null if no trigger</returns>
    public static string? CheckTrigger(UnitState unit, int previousHp, int currentHp)
    {
        // Check if unit has threshold configured
        double threshold = unit.GetDoubleVar(Keys.HpThreshold);
        if (threshold <= 0 || threshold >= 1.0)
            return null;

        // Check if already triggered
        bool alreadyTriggered = unit.GetBoolVar(Keys.ThresholdTriggered, false);
        if (alreadyTriggered)
            return null;

        // Check if skill ID is configured
        if (!unit.Vars.TryGetValue(Keys.ThresholdSkillId, out var skillIdObj) || skillIdObj is not string skillId)
            return null;

        // Check if we have max HP to calculate threshold
        int maxHp = unit.GetIntVar(Keys.MaxHp);
        if (maxHp <= 0)
            return null;

        // Calculate threshold HP value
        int thresholdHp = (int)Math.Round(maxHp * threshold);

        // Check if HP crossed below threshold (was above, now below)
        if (previousHp > thresholdHp && currentHp <= thresholdHp)
        {
            return skillId;
        }

        return null;
    }

    /// <summary>
    /// Marks the threshold as triggered for a unit.
    /// Should be called after successfully executing the threshold skill.
    /// </summary>
    /// <param name="state">Current world state</param>
    /// <param name="unitId">Unit ID to mark</param>
    /// <returns>Updated world state</returns>
    public static WorldState MarkTriggered(WorldState state, string unitId)
    {
        return WorldStateOps.WithUnit(state, unitId, u =>
            u with { Vars = u.Vars.SetItem(Keys.ThresholdTriggered, true) });
    }

    /// <summary>
    /// Checks for and handles HP threshold triggers after damage.
    /// This is a convenience method that combines checking and marking.
    /// </summary>
    /// <param name="state">Current world state</param>
    /// <param name="unitId">Unit ID to check</param>
    /// <param name="previousHp">HP before damage</param>
    /// <returns>(triggeredSkillId, updatedState) - skill ID is null if no trigger</returns>
    public static (string? SkillId, WorldState State) CheckAndPrepare(WorldState state, string unitId, int previousHp)
    {
        var unit = state.GetUnitOrNull(unitId);
        if (unit == null)
            return (null, state);

        int currentHp = unit.GetIntVar(Keys.Hp);
        string? skillId = CheckTrigger(unit, previousHp, currentHp);

        if (skillId != null)
        {
            // Mark as triggered to prevent re-execution
            state = MarkTriggered(state, unitId);
        }

        return (skillId, state);
    }
}
