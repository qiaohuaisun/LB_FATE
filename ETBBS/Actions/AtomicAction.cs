using System.Collections.Immutable;

namespace ETBBS;

/// <summary>
/// A function that transforms a WorldState into a new WorldState.
/// Used to represent compiled atomic actions.
/// </summary>
/// <param name="state">The current world state.</param>
/// <returns>The new world state after applying the effect.</returns>
public delegate WorldState Effect(WorldState state);

/// <summary>
/// Base class for all atomic game actions. An atomic action is the smallest unit of game logic
/// that can be executed independently. Actions are composable, support conflict detection,
/// and can be executed in parallel if they are commutative.
/// </summary>
/// <remarks>
/// Actions declare their dependencies through ReadVars and WriteVars, which enables:
/// - Automatic conflict detection (actions that write to the same variables conflict)
/// - Parallel execution optimization (non-conflicting actions can run concurrently)
/// - Transaction-like semantics (batch execution with rollback on failure)
/// </remarks>
public abstract record AtomicAction
{
    /// <summary>
    /// Compiles this action into an executable effect function.
    /// </summary>
    /// <returns>A function that applies this action to a WorldState.</returns>
    public abstract Effect Compile();

    /// <summary>
    /// Gets the set of variable keys this action reads from.
    /// Used for dependency tracking and conflict detection.
    /// </summary>
    public abstract ImmutableHashSet<string> ReadVars { get; }

    /// <summary>
    /// Gets the set of variable keys this action writes to.
    /// Used for dependency tracking and conflict detection.
    /// </summary>
    public abstract ImmutableHashSet<string> WriteVars { get; }

    /// <summary>
    /// Determines if this action can be safely executed in parallel with another action.
    /// Two actions are commutative if they don't have conflicting reads/writes.
    /// </summary>
    /// <param name="other">The other action to check against.</param>
    /// <returns>True if the actions can be executed in parallel without conflicts.</returns>
    public virtual bool IsCommutativeWith(AtomicAction other)
    {
        // Basic conflict check: no overlapping writes and no RW hazards
        if (!WriteVars.IsDisjoint(other.WriteVars)) return false;
        if (!WriteVars.IsDisjoint(other.ReadVars)) return false;
        if (!ReadVars.IsDisjoint(other.WriteVars)) return false;
        return true;
    }

    /// <summary>Creates a dependency key for a unit variable.</summary>
    protected static string UnitVarKey(string id, string key) => $"unitvar:{id}:{key}";

    /// <summary>Creates a dependency key for a tile variable.</summary>
    protected static string TileVarKey(Coord pos, string key) => $"tilevar:{pos.X},{pos.Y}:{key}";

    /// <summary>Creates a dependency key for a unit tag.</summary>
    protected static string UnitTagKey(string id, string tag) => $"unitag:{id}:{tag}";

    /// <summary>Creates a dependency key for a tile tag.</summary>
    protected static string TileTagKey(Coord pos, string tag) => $"tiltag:{pos.X},{pos.Y}:{tag}";

    /// <summary>Creates a dependency key for a global variable.</summary>
    protected static string GlobalVarKey(string key) => $"globvar:{key}";

    /// <summary>Creates a dependency key for a global tag.</summary>
    protected static string GlobalTagKey(string tag) => $"globtag:{tag}";
}

internal static class SetExtensions
{
    public static bool IsDisjoint<T>(this ImmutableHashSet<T> a, ImmutableHashSet<T> b)
        => a.Count == 0 || b.Count == 0 || a.Intersect(b).IsEmpty;
}

