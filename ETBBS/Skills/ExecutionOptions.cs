namespace ETBBS;

public enum ConflictHandling
{
    WarnOnly,
    BlockOnConflict
}

public sealed record ExecutionOptions(
    bool TransactionalBatch = false,
    ConflictHandling ConflictHandling = ConflictHandling.WarnOnly,
    bool EmitPerActionEventsInTransactional = true
);

