using System.Collections.Immutable;

namespace ETBBS;

public delegate WorldState Effect(WorldState state);

public abstract record AtomicAction
{
    public abstract Effect Compile();

    public abstract ImmutableHashSet<string> ReadVars { get; }
    public abstract ImmutableHashSet<string> WriteVars { get; }

    public virtual bool IsCommutativeWith(AtomicAction other)
    {
        // Basic conflict check: no overlapping writes and no RW hazards
        if (!WriteVars.IsDisjoint(other.WriteVars)) return false;
        if (!WriteVars.IsDisjoint(other.ReadVars)) return false;
        if (!ReadVars.IsDisjoint(other.WriteVars)) return false;
        return true;
    }

    protected static string UnitVarKey(string id, string key) => $"unitvar:{id}:{key}";
    protected static string TileVarKey(Coord pos, string key) => $"tilevar:{pos.X},{pos.Y}:{key}";
    protected static string UnitTagKey(string id, string tag) => $"unitag:{id}:{tag}";
    protected static string TileTagKey(Coord pos, string tag) => $"tiltag:{pos.X},{pos.Y}:{tag}";
    protected static string GlobalVarKey(string key) => $"globvar:{key}";
    protected static string GlobalTagKey(string tag) => $"globtag:{tag}";
}

internal static class SetExtensions
{
    public static bool IsDisjoint<T>(this ImmutableHashSet<T> a, ImmutableHashSet<T> b)
        => a.Count == 0 || b.Count == 0 || a.Intersect(b).IsEmpty;
}
 
