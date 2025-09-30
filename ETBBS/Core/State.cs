using System.Collections.Immutable;

namespace ETBBS;

/// <summary>
/// Represents a 2D coordinate on the game board.
/// </summary>
/// <param name="X">The X coordinate (horizontal position).</param>
/// <param name="Y">The Y coordinate (vertical position).</param>
public readonly record struct Coord(int X, int Y)
{
    /// <summary>
    /// Implicitly converts a tuple to a Coord.
    /// </summary>
    public static implicit operator Coord((int X, int Y) t) => new(t.X, t.Y);

    /// <summary>
    /// Returns a string representation of the coordinate in format "(X,Y)".
    /// </summary>
    public override string ToString() => $"({X},{Y})";
}

/// <summary>
/// Represents the complete immutable state of the game world.
/// This is the root state object that contains all game data.
/// </summary>
/// <param name="Global">Global game state including turn counter and global variables.</param>
/// <param name="Tiles">2D array of tile states representing the game board.</param>
/// <param name="Units">Dictionary mapping unit IDs to their state.</param>
public record WorldState(
    GlobalState Global,
    TileState[,] Tiles,
    ImmutableDictionary<string, UnitState> Units
)
{
    /// <summary>
    /// Creates an empty world state with the specified dimensions.
    /// All tiles are initialized with empty variables and tags.
    /// </summary>
    /// <param name="width">The width of the game board.</param>
    /// <param name="height">The height of the game board.</param>
    /// <returns>A new empty WorldState.</returns>
    public static WorldState CreateEmpty(int width, int height)
    {
        var tiles = new TileState[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tiles[x, y] = new TileState(
                    Vars: ImmutableDictionary<string, object>.Empty,
                    Tags: ImmutableHashSet<string>.Empty
                );
            }
        }

        return new WorldState(
            Global: new GlobalState(
                Turn: 0,
                Vars: ImmutableDictionary<string, object>.Empty,
                Tags: ImmutableHashSet<string>.Empty
            ),
            Tiles: tiles,
            Units: ImmutableDictionary<string, UnitState>.Empty
        );
    }
}

/// <summary>
/// Represents global game state that applies to the entire world.
/// </summary>
/// <param name="Turn">The current turn number.</param>
/// <param name="Vars">Global variables accessible throughout the game.</param>
/// <param name="Tags">Global tags that affect game-wide behavior.</param>
public record GlobalState(
    int Turn,
    ImmutableDictionary<string, object> Vars,
    ImmutableHashSet<string> Tags
);

/// <summary>
/// Represents the state of a single tile on the game board.
/// </summary>
/// <param name="Vars">Tile-specific variables (e.g., terrain type, movement cost).</param>
/// <param name="Tags">Tags that describe tile properties (e.g., "water", "blocked").</param>
public record TileState(
    ImmutableDictionary<string, object> Vars,
    ImmutableHashSet<string> Tags
);

/// <summary>
/// Represents the state of a single unit (character/entity) in the game.
/// </summary>
/// <param name="Vars">Unit variables (e.g., hp, mp, position, stats).</param>
/// <param name="Tags">Tags that describe unit properties (e.g., "stunned", "silenced").</param>
public record UnitState(
    ImmutableDictionary<string, object> Vars,
    ImmutableHashSet<string> Tags
);

/// <summary>
/// Provides functional operations for modifying immutable WorldState.
/// All operations return a new WorldState instance without mutating the original.
/// </summary>
public static class WorldStateOps
{
    /// <summary>
    /// Updates the global state using a transformation function.
    /// </summary>
    /// <param name="s">The current world state.</param>
    /// <param name="f">Function to transform the global state.</param>
    /// <returns>A new WorldState with updated global state.</returns>
    public static WorldState WithGlobal(WorldState s, Func<GlobalState, GlobalState> f)
        => s with { Global = f(s.Global) };

    /// <summary>
    /// Updates a unit's state using a transformation function.
    /// If the unit doesn't exist, creates a new unit with empty state first.
    /// </summary>
    /// <param name="s">The current world state.</param>
    /// <param name="id">The unit ID to update.</param>
    /// <param name="f">Function to transform the unit state.</param>
    /// <returns>A new WorldState with updated unit state.</returns>
    public static WorldState WithUnit(WorldState s, string id, Func<UnitState, UnitState> f)
    {
        var old = s.Units.TryGetValue(id, out var u)
            ? u
            : new UnitState(ImmutableDictionary<string, object>.Empty, ImmutableHashSet<string>.Empty);
        var @new = f(old);
        return s with { Units = s.Units.SetItem(id, @new) };
    }

    /// <summary>
    /// Updates a tile's state at the specified position using a transformation function.
    /// Returns the original state unchanged if the position is out of bounds.
    /// </summary>
    /// <param name="s">The current world state.</param>
    /// <param name="pos">The coordinate of the tile to update.</param>
    /// <param name="f">Function to transform the tile state.</param>
    /// <returns>A new WorldState with updated tile state.</returns>
    public static WorldState WithTile(WorldState s, Coord pos, Func<TileState, TileState> f)
    {
        var (x, y) = (pos.X, pos.Y);
        var width = s.Tiles.GetLength(0);
        var height = s.Tiles.GetLength(1);
        if (x < 0 || x >= width || y < 0 || y >= height)
            return s; // out of bounds: no-op

        var clone = (TileState[,])s.Tiles.Clone();
        clone[x, y] = f(clone[x, y]);
        return s with { Tiles = clone };
    }
}

