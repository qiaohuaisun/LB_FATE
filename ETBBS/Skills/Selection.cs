using System.Linq;

namespace ETBBS;

public static class Selection
{
    public static Func<Context, string?> ById(string id) => ctx => id;

    public static Func<Context, IEnumerable<string>> UnitsWithTag(string tag)
        => ctx => ctx.State.Units.Where(kv => kv.Value.Tags.Contains(tag)).Select(kv => kv.Key);

    public static Func<Context, IEnumerable<string>> Allies(string casterId, IReadOnlyDictionary<string, string> teamMap)
        => ctx =>
        {
            if (!teamMap.TryGetValue(casterId, out var team)) return Array.Empty<string>();
            return ctx.State.Units.Keys.Where(id => teamMap.TryGetValue(id, out var t) && t == team);
        };

    public static Func<Context, IEnumerable<string>> Enemies(string casterId, IReadOnlyDictionary<string, string> teamMap)
        => ctx =>
        {
            if (!teamMap.TryGetValue(casterId, out var team)) return Array.Empty<string>();
            return ctx.State.Units.Keys.Where(id => teamMap.TryGetValue(id, out var t) && t != team);
        };

    public static Func<Context, IEnumerable<string>> WithinRange(string originUnitId, int range, DistanceMetric metric = DistanceMetric.Manhattan, string posKey = Keys.Pos)
        => ctx =>
        {
            if (!TryGetPos(ctx, originUnitId, posKey, out var o)) return Array.Empty<string>();
            return ctx.State.Units.Keys.Where(id => id != originUnitId && TryGetPos(ctx, id, posKey, out var p) && Dist(o, p, metric) <= range);
        };

    public static Func<Context, IEnumerable<string>> EnemiesWithinRange(string casterId, IReadOnlyDictionary<string, string> teamMap, int range, DistanceMetric metric = DistanceMetric.Manhattan, string posKey = Keys.Pos)
        => ctx =>
        {
            if (!teamMap.TryGetValue(casterId, out var team)) return Array.Empty<string>();
            if (!TryGetPos(ctx, casterId, posKey, out var o)) return Array.Empty<string>();
            return ctx.State.Units.Keys.Where(id =>
                id != casterId && teamMap.TryGetValue(id, out var t) && t != team &&
                TryGetPos(ctx, id, posKey, out var p) && Dist(o, p, metric) <= range);
        };

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
 


