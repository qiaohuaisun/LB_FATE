namespace ETBBS;

public sealed record ReplayRecord(
    WorldState Initial,
    IReadOnlyList<AtomicAction[]> Steps,
    WorldState Final,
    IReadOnlyList<IReadOnlyList<string>> Logs
);

public sealed class ReplaySystem
{
    private readonly SkillExecutor _executor = new();

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

