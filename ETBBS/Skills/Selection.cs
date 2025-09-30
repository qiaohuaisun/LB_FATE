namespace ETBBS;

/// <summary>
/// Provides target selection functions for skill execution.
/// All methods return selectors that operate on immutable Context.
/// </summary>
public static class Selection
{
    /// <summary>
    /// Selects a specific unit by ID.
    /// </summary>
    /// <param name="id">The unit ID to select.</param>
    /// <returns>A selector function that always returns the specified ID.</returns>
    public static Func<Context, string?> ById(string id) => ctx => id;

    /// <summary>
    /// Selects all units that have a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>A selector function that returns unit IDs with the specified tag.</returns>
    public static Func<Context, IEnumerable<string>> UnitsWithTag(string tag)
        => ctx => ctx.State.Units.Where(kv => kv.Value.Tags.Contains(tag)).Select(kv => kv.Key);

    /// <summary>
    /// Selects all allies of the specified caster (units on the same team).
    /// </summary>
    /// <param name="casterId">The casting unit's ID.</param>
    /// <param name="teamMap">Mapping of unit ID to team name.</param>
    /// <returns>A selector function that returns ally unit IDs.</returns>
    public static Func<Context, IEnumerable<string>> Allies(string casterId, IReadOnlyDictionary<string, string> teamMap)
        => ctx =>
        {
            if (!teamMap.TryGetValue(casterId, out var team)) return Array.Empty<string>();
            return ctx.State.Units.Keys.Where(id => teamMap.TryGetValue(id, out var t) && t == team);
        };

    /// <summary>
    /// Selects all enemies of the specified caster (units on different teams).
    /// </summary>
    /// <param name="casterId">The casting unit's ID.</param>
    /// <param name="teamMap">Mapping of unit ID to team name.</param>
    /// <returns>A selector function that returns enemy unit IDs.</returns>
    public static Func<Context, IEnumerable<string>> Enemies(string casterId, IReadOnlyDictionary<string, string> teamMap)
        => ctx =>
        {
            if (!teamMap.TryGetValue(casterId, out var team)) return Array.Empty<string>();
            return ctx.State.Units.Keys.Where(id => teamMap.TryGetValue(id, out var t) && t != team);
        };

    /// <summary>
    /// Selects all units within a specified range of the origin unit.
    /// </summary>
    /// <param name="originUnitId">The origin unit ID.</param>
    /// <param name="range">Maximum distance.</param>
    /// <param name="metric">Distance calculation method (default: Manhattan).</param>
    /// <param name="posKey">Variable key for unit position (default: Keys.Pos).</param>
    /// <returns>A selector function that returns unit IDs within range.</returns>
    public static Func<Context, IEnumerable<string>> WithinRange(string originUnitId, int range, DistanceMetric metric = DistanceMetric.Manhattan, string posKey = Keys.Pos)
        => ctx =>
        {
            if (!TryGetPos(ctx, originUnitId, posKey, out var o)) return Array.Empty<string>();
            return ctx.State.Units.Keys.Where(id => id != originUnitId && TryGetPos(ctx, id, posKey, out var p) && Dist(o, p, metric) <= range);
        };

    /// <summary>
    /// Selects enemy units within range of the caster.
    /// Combines Enemies() and WithinRange() filters.
    /// </summary>
    /// <param name="casterId">The casting unit's ID.</param>
    /// <param name="teamMap">Mapping of unit ID to team name.</param>
    /// <param name="range">Maximum distance.</param>
    /// <param name="metric">Distance calculation method (default: Manhattan).</param>
    /// <param name="posKey">Variable key for unit position (default: Keys.Pos).</param>
    /// <returns>A selector function that returns enemy unit IDs within range.</returns>
    public static Func<Context, IEnumerable<string>> EnemiesWithinRange(string casterId, IReadOnlyDictionary<string, string> teamMap, int range, DistanceMetric metric = DistanceMetric.Manhattan, string posKey = Keys.Pos)
        => ctx =>
        {
            if (!teamMap.TryGetValue(casterId, out var team)) return Array.Empty<string>();
            if (!TryGetPos(ctx, casterId, posKey, out var o)) return Array.Empty<string>();
            return ctx.State.Units.Keys.Where(id =>
                id != casterId && teamMap.TryGetValue(id, out var t) && t != team &&
                TryGetPos(ctx, id, posKey, out var p) && Dist(o, p, metric) <= range);
        };

    /// <summary>
    /// Sorts units by distance to the target unit (nearest first).
    /// </summary>
    /// <param name="targetUnitId">The reference unit ID.</param>
    /// <param name="metric">Distance calculation method (default: Manhattan).</param>
    /// <param name="posKey">Variable key for unit position (default: Keys.Pos).</param>
    /// <returns>A selector function that returns unit IDs sorted by distance.</returns>
    public static Func<Context, IEnumerable<string>> SortByNearestTo(string targetUnitId, DistanceMetric metric = DistanceMetric.Manhattan, string posKey = Keys.Pos)
        => ctx =>
        {
            if (!TryGetPos(ctx, targetUnitId, posKey, out var o)) return Array.Empty<string>();
            return ctx.State.Units.Keys
                .Where(id => id != targetUnitId && TryGetPos(ctx, id, posKey, out var _))
                .OrderBy(id =>
                {
                    TryGetPos(ctx, id, posKey, out var p);
                    return Dist(o, p, metric);
                });
        };

    /// <summary>
    /// Orders units by a numeric variable value.
    /// Units without the variable are sorted last (ascending) or first (descending).
    /// </summary>
    /// <param name="key">The unit variable key to sort by.</param>
    /// <param name="ascending">If true, sort low to high; if false, sort high to low.</param>
    /// <returns>A selector function that returns sorted unit IDs.</returns>
    public static Func<Context, IEnumerable<string>> OrderByUnitVarInt(string key, bool ascending = true)
        => ctx =>
        {
            IEnumerable<KeyValuePair<string, UnitState>> seq = ctx.State.Units;
            Func<KeyValuePair<string, UnitState>, int> keySel = kv =>
            {
                if (!kv.Value.Vars.TryGetValue(key, out var v)) return int.MaxValue;
                return v switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => int.MaxValue };
            };
            return (ascending ? seq.OrderBy(keySel) : seq.OrderByDescending(keySel)).Select(kv => kv.Key);
        };

    /// <summary>
    /// Selects units ordered by HP (lowest first).
    /// Convenient alias for OrderByUnitVarInt(Keys.Hp, ascending: true).
    /// </summary>
    /// <returns>A selector function that returns unit IDs sorted by HP (ascending).</returns>
    public static Func<Context, IEnumerable<string>> LowestHp()
        => OrderByUnitVarInt(Keys.Hp, ascending: true);

    private static int Dist(Coord a, Coord b, DistanceMetric m)
        => m switch
        {
            DistanceMetric.Manhattan => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y),
            DistanceMetric.Chebyshev => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)),
            DistanceMetric.Euclidean => (int)Math.Round(Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y))),
            _ => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)
        };

    private static bool TryGetPos(Context ctx, string id, string posKey, out Coord pos)
    {
        pos = ctx.GetUnitVar<Coord>(id, posKey, default);
        return !pos.Equals(default(Coord));
    }
}



