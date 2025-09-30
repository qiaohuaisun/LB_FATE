namespace ETBBS;

/// <summary>
/// Determines how conflicts between concurrent actions are handled during execution.
/// </summary>
public enum ConflictHandling
{
    /// <summary>
    /// Log conflicts as warnings but continue execution (default).
    /// </summary>
    WarnOnly,

    /// <summary>
    /// Abort execution when a conflict is detected.
    /// </summary>
    BlockOnConflict
}

/// <summary>
/// Configuration options for atomic action execution.
/// </summary>
/// <param name="TransactionalBatch">
/// If true, all actions execute atomically: either all succeed or all fail together.
/// If false, actions execute independently and failures are isolated.
/// </param>
/// <param name="ConflictHandling">
/// Strategy for handling dependency conflicts between actions (default: WarnOnly).
/// </param>
/// <param name="EmitPerActionEventsInTransactional">
/// If true, emit individual action events even in transactional mode.
/// If false, only emit a single batch completion event (default: true).
/// </param>
public sealed record ExecutionOptions(
    bool TransactionalBatch = false,
    ConflictHandling ConflictHandling = ConflictHandling.WarnOnly,
    bool EmitPerActionEventsInTransactional = true
);

