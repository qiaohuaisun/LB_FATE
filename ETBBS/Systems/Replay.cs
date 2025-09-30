namespace ETBBS;

/// <summary>
/// Immutable record of a complete game replay.
/// Contains initial state, all action steps, final state, and execution logs.
/// </summary>
/// <param name="Initial">World state at the start of replay.</param>
/// <param name="Steps">Sequence of action batches executed.</param>
/// <param name="Final">World state at the end of replay.</param>
/// <param name="Logs">Execution logs for each step (parallel to Steps).</param>
public sealed record ReplayRecord(
    WorldState Initial,
    IReadOnlyList<AtomicAction[]> Steps,
    WorldState Final,
    IReadOnlyList<IReadOnlyList<string>> Logs
);

/// <summary>
/// System for recording and replaying game sequences.
/// Useful for debugging, testing, and implementing replay/undo features.
/// </summary>
public sealed class ReplaySystem
{
    private readonly SkillExecutor _executor = new();

    /// <summary>
    /// Executes a sequence of action batches and records the full replay.
    /// </summary>
    /// <param name="initial">Starting world state.</param>
    /// <param name="steps">Action batches to execute sequentially.</param>
    /// <returns>Complete replay record with logs.</returns>
    public ReplayRecord Record(WorldState initial, List<AtomicAction[]> steps)
    {
        var logs = new List<IReadOnlyList<string>>();
        var state = initial;
        foreach (var batch in steps)
        {
            (state, var log) = _executor.Execute(state, batch);
            logs.Add(log.Messages);
        }
        return new ReplayRecord(initial, steps, state, logs);
    }
}

