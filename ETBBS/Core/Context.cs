namespace ETBBS;

/// <summary>
/// Provides a read-only view of the world state with convenient accessor methods.
/// This struct wraps WorldState to provide type-safe access to unit, tile, and global variables.
/// </summary>
/// <remarks>
/// Context is a lightweight readonly struct that doesn't copy the underlying WorldState.
/// It's designed for efficient querying during skill execution and game logic.
/// All methods are null-safe and return default values for missing data.
/// </remarks>
public readonly struct Context
{
    private readonly WorldState _state;

    /// <summary>
    /// Creates a new Context wrapping the specified world state.
    /// </summary>
    /// <param name="state">The world state to wrap.</param>
    public Context(WorldState state) => _state = state;

    /// <summary>
    /// Gets the underlying world state.
    /// </summary>
    public WorldState State => _state;

    /// <summary>
    /// Gets a typed variable from a unit, returning a default value if not found.
    /// </summary>
    /// <typeparam name="T">The expected type of the variable.</typeparam>
    /// <param name="id">The unit ID.</param>
    /// <param name="key">The variable key.</param>
    /// <param name="defaultValue">The value to return if the variable doesn't exist or has wrong type.</param>
    /// <returns>The variable value or the default value.</returns>
    public T GetUnitVar<T>(string id, string key, T defaultValue = default!)
    {
        if (_state.Units.TryGetValue(id, out var u) && u.Vars.TryGetValue(key, out var obj) && obj is T t)
            return t;
        return defaultValue!;
    }

    /// <summary>
    /// Attempts to get a typed variable from a unit.
    /// </summary>
    /// <typeparam name="T">The expected type of the variable.</typeparam>
    /// <param name="id">The unit ID.</param>
    /// <param name="key">The variable key.</param>
    /// <param name="value">The output variable value if successful.</param>
    /// <returns>True if the variable exists and has the correct type; otherwise false.</returns>
    public bool TryGetUnitVar<T>(string id, string key, out T value)
    {
        if (_state.Units.TryGetValue(id, out var u) && u.Vars.TryGetValue(key, out var obj) && obj is T t)
        { value = t; return true; }
        value = default!;
        return false;
    }

    /// <summary>
    /// Checks if a unit has a specific tag.
    /// </summary>
    /// <param name="id">The unit ID.</param>
    /// <param name="tag">The tag to check.</param>
    /// <returns>True if the unit exists and has the tag; otherwise false.</returns>
    public bool HasUnitTag(string id, string tag)
        => _state.Units.TryGetValue(id, out var u) && u.Tags.Contains(tag);

    /// <summary>
    /// Gets a typed variable from a tile, returning a default value if not found.
    /// </summary>
    /// <typeparam name="T">The expected type of the variable.</typeparam>
    /// <param name="pos">The tile coordinate.</param>
    /// <param name="key">The variable key.</param>
    /// <param name="defaultValue">The value to return if the variable doesn't exist or has wrong type.</param>
    /// <returns>The variable value or the default value.</returns>
    public T GetTileVar<T>(Coord pos, string key, T defaultValue = default!)
    {
        var (x, y) = (pos.X, pos.Y);
        var w = _state.Tiles.GetLength(0);
        var h = _state.Tiles.GetLength(1);
        if (x < 0 || x >= w || y < 0 || y >= h) return defaultValue!;
        var tile = _state.Tiles[x, y];
        return tile.Vars.TryGetValue(key, out var obj) && obj is T t ? t : defaultValue!;
    }

    /// <summary>
    /// Attempts to get a typed variable from a tile.
    /// </summary>
    /// <typeparam name="T">The expected type of the variable.</typeparam>
    /// <param name="pos">The tile coordinate.</param>
    /// <param name="key">The variable key.</param>
    /// <param name="value">The output variable value if successful.</param>
    /// <returns>True if the tile exists and the variable has the correct type; otherwise false.</returns>
    public bool TryGetTileVar<T>(Coord pos, string key, out T value)
    {
        var (x, y) = (pos.X, pos.Y);
        var w = _state.Tiles.GetLength(0);
        var h = _state.Tiles.GetLength(1);
        if (x >= 0 && x < w && y >= 0 && y < h)
        {
            var tile = _state.Tiles[x, y];
            if (tile.Vars.TryGetValue(key, out var obj) && obj is T t)
            { value = t; return true; }
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// Checks if a tile has a specific tag.
    /// </summary>
    /// <param name="pos">The tile coordinate.</param>
    /// <param name="tag">The tag to check.</param>
    /// <returns>True if the tile exists and has the tag; otherwise false.</returns>
    public bool HasTileTag(Coord pos, string tag)
    {
        var (x, y) = (pos.X, pos.Y);
        var w = _state.Tiles.GetLength(0);
        var h = _state.Tiles.GetLength(1);
        if (x < 0 || x >= w || y < 0 || y >= h) return false;
        return _state.Tiles[x, y].Tags.Contains(tag);
    }

    /// <summary>
    /// Gets a typed global variable, returning a default value if not found.
    /// </summary>
    /// <typeparam name="T">The expected type of the variable.</typeparam>
    /// <param name="key">The variable key.</param>
    /// <param name="defaultValue">The value to return if the variable doesn't exist or has wrong type.</param>
    /// <returns>The variable value or the default value.</returns>
    public T GetGlobalVar<T>(string key, T defaultValue = default!)
        => _state.Global.Vars.TryGetValue(key, out var obj) && obj is T t ? t : defaultValue!;

    /// <summary>
    /// Attempts to get a typed global variable.
    /// </summary>
    /// <typeparam name="T">The expected type of the variable.</typeparam>
    /// <param name="key">The variable key.</param>
    /// <param name="value">The output variable value if successful.</param>
    /// <returns>True if the variable exists and has the correct type; otherwise false.</returns>
    public bool TryGetGlobalVar<T>(string key, out T value)
    {
        if (_state.Global.Vars.TryGetValue(key, out var obj) && obj is T t)
        { value = t; return true; }
        value = default!;
        return false;
    }

    /// <summary>
    /// Checks if a global tag is present.
    /// </summary>
    /// <param name="tag">The tag to check.</param>
    /// <returns>True if the global tag exists; otherwise false.</returns>
    public bool HasGlobalTag(string tag) => _state.Global.Tags.Contains(tag);

    // ===== Convenience Accessors =====

    /// <summary>
    /// Attempts to get a unit's position.
    /// </summary>
    /// <param name="id">The unit ID.</param>
    /// <param name="pos">The output position if successful.</param>
    /// <returns>True if the unit exists and has a position; otherwise false.</returns>
    public bool TryGetUnitPos(string id, out Coord pos) => TryGetUnitVar(id, Keys.Pos, out pos);

    /// <summary>
    /// Gets a unit's position or returns a default coordinate.
    /// </summary>
    /// <param name="id">The unit ID.</param>
    /// <param name="defaultPos">The default position to return if not found.</param>
    /// <returns>The unit's position or the default position.</returns>
    public Coord GetUnitPosOrDefault(string id, Coord defaultPos = default) => GetUnitVar(id, Keys.Pos, defaultPos);
}

