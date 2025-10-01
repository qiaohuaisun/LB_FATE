namespace ETBBS;

public static class EventTopics
{
    public const string BatchStart = "batch.start";
    public const string BatchEnd = "batch.end";
    public const string ValidationFailed = "validation.failed";
    public const string ConflictDetected = "conflict.detected";
    public const string ActionExecuting = "action.executing";
    public const string ActionExecuted = "action.executed";

    public const string TurnStart = "turn.start";
    public const string TurnEnd = "turn.end";
    public const string UnitMpRegen = "unit.mp.regen";
    public const string UndyingTick = "status.undying.tick";
    public const string UndyingEnd = "status.undying.end";

    public const string UnitDamaged = "unit.damaged";
    public const string UnitDied = "unit.died";
    public const string UnitMoved = "unit.moved";
    public const string UnitTagAdded = "unit.tag.added";
    public const string UnitTagRemoved = "unit.tag.removed";
    public const string TileTagAdded = "tile.tag.added";
    public const string TileTagRemoved = "tile.tag.removed";
    public const string UnitVarChanged = "unit.var.changed";
    public const string TileVarChanged = "tile.var.changed";
    public const string GlobalVarChanged = "global.var.changed";
    public const string GlobalTagAdded = "global.tag.added";
    public const string GlobalTagRemoved = "global.tag.removed";
}

public sealed record BatchEvent(int Index);
public sealed record ValidationFailedEvent(string Reason, AtomicAction[] Actions);
public sealed record ConflictDetectedEvent(int IndexA, int IndexB, AtomicAction A, AtomicAction B);
public sealed record ActionExecutingEvent(Context Context, AtomicAction Action);
public sealed record ActionExecutedEvent(WorldState Before, WorldState After, AtomicAction Action);

public sealed record UnitDamagedEvent(string UnitId, int BeforeHp, int AfterHp, int Amount);
public sealed record UnitDiedEvent(string UnitId);
public sealed record UnitMovedEvent(string UnitId, Coord Before, Coord After);
public sealed record UnitTagEvent(string UnitId, string Tag, bool Added);
public sealed record TileTagEvent(Coord Pos, string Tag, bool Added);
public sealed record VarChangedEvent(string Scope, string Key, object? Before, object? After, string? UnitId = null, Coord? Pos = null);

