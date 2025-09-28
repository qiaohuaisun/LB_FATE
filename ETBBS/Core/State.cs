using System.Collections.Immutable;

namespace ETBBS;

public readonly record struct Coord(int X, int Y)
{
    public static implicit operator Coord((int X, int Y) t) => new(t.X, t.Y);
    public override string ToString() => $"({X},{Y})";
}

public record WorldState(
    GlobalState Global,
    TileState[,] Tiles,
    ImmutableDictionary<string, UnitState> Units
)
{
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

public record GlobalState(
    int Turn,
    ImmutableDictionary<string, object> Vars,
    ImmutableHashSet<string> Tags
);

public record TileState(
    ImmutableDictionary<string, object> Vars,
    ImmutableHashSet<string> Tags
);

public record UnitState(
    ImmutableDictionary<string, object> Vars,
    ImmutableHashSet<string> Tags
);

public static class WorldStateOps
{
    public static WorldState WithGlobal(WorldState s, Func<GlobalState, GlobalState> f)
        => s with { Global = f(s.Global) };

    public static WorldState WithUnit(WorldState s, string id, Func<UnitState, UnitState> f)
    {
        var old = s.Units.TryGetValue(id, out var u)
            ? u
            : new UnitState(ImmutableDictionary<string, object>.Empty, ImmutableHashSet<string>.Empty);
        var @new = f(old);
        return s with { Units = s.Units.SetItem(id, @new) };
    }

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

