namespace ETBBS;

// Note: using readonly struct for broad compatibility. Can change to ref struct if needed.
public readonly struct Context
{
    private readonly WorldState _state;

    public Context(WorldState state) => _state = state;

    public WorldState State => _state;

    public T GetUnitVar<T>(string id, string key, T defaultValue = default!)
    {
        if (_state.Units.TryGetValue(id, out var u) && u.Vars.TryGetValue(key, out var obj) && obj is T t)
            return t;
        return defaultValue!;
    }

    public bool TryGetUnitVar<T>(string id, string key, out T value)
    {
        if (_state.Units.TryGetValue(id, out var u) && u.Vars.TryGetValue(key, out var obj) && obj is T t)
        { value = t; return true; }
        value = default!;
        return false;
    }

    public bool HasUnitTag(string id, string tag)
        => _state.Units.TryGetValue(id, out var u) && u.Tags.Contains(tag);

    public T GetTileVar<T>(Coord pos, string key, T defaultValue = default!)
    {
        var (x, y) = (pos.X, pos.Y);
        var w = _state.Tiles.GetLength(0);
        var h = _state.Tiles.GetLength(1);
        if (x < 0 || x >= w || y < 0 || y >= h) return defaultValue!;
        var tile = _state.Tiles[x, y];
        return tile.Vars.TryGetValue(key, out var obj) && obj is T t ? t : defaultValue!;
    }

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

    public bool HasTileTag(Coord pos, string tag)
    {
        var (x, y) = (pos.X, pos.Y);
        var w = _state.Tiles.GetLength(0);
        var h = _state.Tiles.GetLength(1);
        if (x < 0 || x >= w || y < 0 || y >= h) return false;
        return _state.Tiles[x, y].Tags.Contains(tag);
    }

    public T GetGlobalVar<T>(string key, T defaultValue = default!)
        => _state.Global.Vars.TryGetValue(key, out var obj) && obj is T t ? t : defaultValue!;

    public bool TryGetGlobalVar<T>(string key, out T value)
    {
        if (_state.Global.Vars.TryGetValue(key, out var obj) && obj is T t)
        { value = t; return true; }
        value = default!;
        return false;
    }

    public bool HasGlobalTag(string tag) => _state.Global.Tags.Contains(tag);

    // Convenience accessors for conventional keys
    public bool TryGetUnitPos(string id, out Coord pos) => TryGetUnitVar(id, Keys.Pos, out pos);
    public Coord GetUnitPosOrDefault(string id, Coord defaultPos = default) => GetUnitVar(id, Keys.Pos, defaultPos);
}

