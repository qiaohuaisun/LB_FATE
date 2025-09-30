using System.Collections.Immutable;

namespace ETBBS;

/// <summary>
/// Safe access extension methods to prevent null reference exceptions and KeyNotFoundExceptions.
/// Provides defensive programming helpers for working with WorldState.
/// </summary>
public static class SafeAccess
{
    /// <summary>
    /// Safely retrieves a unit from the world state, returning null if not found.
    /// </summary>
    /// <param name="state">The world state to query.</param>
    /// <param name="id">The unit ID to look up.</param>
    /// <returns>The unit if found, null otherwise.</returns>
    public static UnitState? GetUnitOrNull(this WorldState state, string id) =>
        state.Units.TryGetValue(id, out var unit) ? unit : null;

    /// <summary>
    /// Safely checks if a unit exists in the world state.
    /// </summary>
    /// <param name="state">The world state to query.</param>
    /// <param name="id">The unit ID to check.</param>
    /// <returns>True if the unit exists, false otherwise.</returns>
    public static bool HasUnit(this WorldState state, string id) =>
        state.Units.ContainsKey(id);

    /// <summary>
    /// Safely retrieves a variable from a unit, returning a default value if not found.
    /// </summary>
    /// <param name="unit">The unit to query.</param>
    /// <param name="key">The variable key.</param>
    /// <param name="defaultValue">Value to return if the variable doesn't exist.</param>
    /// <returns>The variable value or default value.</returns>
    public static object? GetVarOrDefault(this UnitState unit, string key, object? defaultValue = null) =>
        unit.Vars.TryGetValue(key, out var value) ? value : defaultValue;

    /// <summary>
    /// Safely retrieves a variable from global state, returning a default value if not found.
    /// </summary>
    /// <param name="global">The global state to query.</param>
    /// <param name="key">The variable key.</param>
    /// <param name="defaultValue">Value to return if the variable doesn't exist.</param>
    /// <returns>The variable value or default value.</returns>
    public static object? GetVarOrDefault(this GlobalState global, string key, object? defaultValue = null) =>
        global.Vars.TryGetValue(key, out var value) ? value : defaultValue;

    /// <summary>
    /// Safely checks if a tile position is within bounds of the world state.
    /// </summary>
    /// <param name="state">The world state to check.</param>
    /// <param name="pos">The coordinate to check.</param>
    /// <returns>True if the position is valid, false otherwise.</returns>
    public static bool IsValidPosition(this WorldState state, Coord pos)
    {
        int width = state.Tiles.GetLength(0);
        int height = state.Tiles.GetLength(1);
        return pos.X >= 0 && pos.X < width && pos.Y >= 0 && pos.Y < height;
    }

    /// <summary>
    /// Safely retrieves a tile state, returning null if out of bounds.
    /// </summary>
    /// <param name="state">The world state to query.</param>
    /// <param name="pos">The coordinate to look up.</param>
    /// <returns>The tile state if in bounds, null otherwise.</returns>
    public static TileState? GetTileOrNull(this WorldState state, Coord pos)
    {
        if (!state.IsValidPosition(pos))
            return null;
        return state.Tiles[pos.X, pos.Y];
    }

    /// <summary>
    /// Safely gets a variable from a dictionary using GetValueOrDefault pattern.
    /// </summary>
    public static object? GetValueOrDefault(this ImmutableDictionary<string, object> dict, string key, object? defaultValue = null) =>
        dict.TryGetValue(key, out var value) ? value : defaultValue;

    /// <summary>
    /// Safely checks if a unit is alive (has positive HP).
    /// Returns false if unit doesn't exist or HP variable is missing.
    /// </summary>
    /// <param name="state">The world state to query.</param>
    /// <param name="id">The unit ID to check.</param>
    /// <returns>True if unit exists and has HP > 0, false otherwise.</returns>
    public static bool IsUnitAlive(this WorldState state, string id)
    {
        var unit = state.GetUnitOrNull(id);
        if (unit == null)
            return false;

        int hp = unit.GetIntVar(Keys.Hp, 0);
        return hp > 0;
    }

    /// <summary>
    /// Safely checks if a unit has a specific tag.
    /// Returns false if unit doesn't exist.
    /// </summary>
    /// <param name="state">The world state to query.</param>
    /// <param name="id">The unit ID to check.</param>
    /// <param name="tag">The tag to look for.</param>
    /// <returns>True if unit exists and has the tag, false otherwise.</returns>
    public static bool UnitHasTag(this WorldState state, string id, string tag)
    {
        var unit = state.GetUnitOrNull(id);
        return unit?.Tags.Contains(tag) ?? false;
    }

    /// <summary>
    /// Safely gets the position of a unit, returning a default coordinate if not found.
    /// </summary>
    /// <param name="state">The world state to query.</param>
    /// <param name="id">The unit ID to look up.</param>
    /// <param name="defaultPos">Position to return if unit or position doesn't exist.</param>
    /// <returns>The unit's position or default position.</returns>
    public static Coord GetUnitPosition(this WorldState state, string id, Coord defaultPos = default)
    {
        var unit = state.GetUnitOrNull(id);
        if (unit == null)
            return defaultPos;

        if (!unit.Vars.TryGetValue(Keys.Pos, out var posValue) || posValue is not Coord pos)
            return defaultPos;

        return pos;
    }
}