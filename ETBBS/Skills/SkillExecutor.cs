namespace ETBBS;

public delegate bool ActionValidator(Context ctx, AtomicAction[] actions, out string? reason);

public record ExecutionLog(List<string> Messages)
{
    public void Info(string msg) => Messages.Add($"INFO: {msg}");
    public void Warn(string msg) => Messages.Add($"WARN: {msg}");
    public void Error(string msg) => Messages.Add($"ERROR: {msg}");
}

public sealed class SkillExecutor
{
    public (WorldState, ExecutionLog) Execute(
        WorldState state,
        AtomicAction[] actions,
        ActionValidator? validator = null,
        EventBus? events = null,
        ExecutionOptions? options = null)
    {
        options ??= new ExecutionOptions();
        var log = new ExecutionLog(new List<string>());
        var ctx = new Context(state);

        if (validator != null && !validator(ctx, actions, out var reason))
        {
            log.Warn($"Validation rejected actions: {reason ?? "unspecified"}.");
            events?.Publish(EventTopics.ValidationFailed, new ValidationFailedEvent(reason ?? "unspecified", actions));
            return (state, log);
        }

        // Simple conflict reporting (no reordering)
        for (int i = 0; i < actions.Length; i++)
        {
            for (int j = i + 1; j < actions.Length; j++)
            {
                if (!actions[i].IsCommutativeWith(actions[j]))
                {
                    var msg = $"Potential conflict between {actions[i].GetType().Name} and {actions[j].GetType().Name}.";
                    if (options.ConflictHandling == ConflictHandling.BlockOnConflict)
                    {
                        log.Error(msg);
                        events?.Publish(EventTopics.ConflictDetected, new ConflictDetectedEvent(i, j, actions[i], actions[j]));
                        return (state, log);
                    }
                    else
                    {
                        log.Warn(msg);
                        events?.Publish(EventTopics.ConflictDetected, new ConflictDetectedEvent(i, j, actions[i], actions[j]));
                    }
                }
            }
        }

        if (!options.TransactionalBatch)
        {
            // Execute sequentially
            var cur = state;
            foreach (var action in actions)
            {
                events?.Publish(EventTopics.ActionExecuting, new ActionExecutingEvent(new Context(cur), action));
                var before = cur;
                var eff = action.Compile();
                cur = eff(cur);
                events?.Publish(EventTopics.ActionExecuted, new ActionExecutedEvent(before, cur, action));

                // Specialized hooks
                TryPublishSpecializedEvents(events, before, cur, action);
                log.Info($"Executed {action}.");
            }

            return (cur, log);
        }
        else
        {
            // Transactional: compute changes against the same base and apply once
            var baseState = state;
            var writes = new Dictionary<string, object?>();
            var tagPresence = new Dictionary<string, bool>();
            var writeOwner = new Dictionary<string, int>();

            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (options.EmitPerActionEventsInTransactional)
                    events?.Publish(EventTopics.ActionExecuting, new ActionExecutingEvent(new Context(baseState), action));

                var after = action.Compile()(baseState);

                foreach (var key in action.WriteVars)
                {
                    if (IsTagKey(key))
                    {
                        var present = ReadTagPresence(after, key);
                        if (writeOwner.TryGetValue(key, out var prev))
                        {
                            if (options.ConflictHandling == ConflictHandling.BlockOnConflict && prev != i)
                            {
                                log.Error($"Transactional write conflict on '{key}' by action #{i} and #{prev}.");
                                return (state, log);
                            }
                        }
                        tagPresence[key] = present;
                        writeOwner[key] = i;
                    }
                    else
                    {
                        var value = ReadVarValue(after, key);
                        if (writeOwner.TryGetValue(key, out var prev))
                        {
                            if (options.ConflictHandling == ConflictHandling.BlockOnConflict && prev != i)
                            {
                                log.Error($"Transactional write conflict on '{key}' by action #{i} and #{prev}.");
                                return (state, log);
                            }
                        }
                        writes[key] = value;
                        writeOwner[key] = i;
                    }
                }

                if (options.EmitPerActionEventsInTransactional)
                {
                    events?.Publish(EventTopics.ActionExecuted, new ActionExecutedEvent(baseState, after, action));
                    TryPublishSpecializedEvents(events, baseState, after, action);
                }
                log.Info($"Prepared {action}.");
            }

            // Apply aggregated writes
            var cur = baseState;
            foreach (var kv in writes)
                cur = ApplyVarWrite(cur, kv.Key, kv.Value);
            foreach (var kv in tagPresence)
                cur = ApplyTagWrite(cur, kv.Key, kv.Value);

            return (cur, log);
        }
    }

    public (WorldState, ExecutionLog) ExecutePlan(
        WorldState state,
        IReadOnlyList<AtomicAction[]> plan,
        ActionValidator? validator = null,
        EventBus? events = null,
        ExecutionOptions? options = null)
    {
        var all = new ExecutionLog(new List<string>());
        var cur = state;
        for (int i = 0; i < plan.Count; i++)
        {
            var batch = plan[i];
            events?.Publish(EventTopics.BatchStart, new BatchEvent(i));
            (cur, var log) = Execute(cur, batch, validator, events, options);
            all.Messages.AddRange(log.Messages);
            events?.Publish(EventTopics.BatchEnd, new BatchEvent(i));
        }
        return (cur, all);
    }

    private static bool IsTagKey(string key)
        => key.StartsWith("unitag:") || key.StartsWith("tiltag:") || key.StartsWith("globtag:");

    private static object? ReadVarValue(WorldState s, string writeKey)
    {
        if (writeKey.StartsWith("unitvar:"))
        {
            var rest = writeKey.Substring("unitvar:".Length);
            var idx = rest.IndexOf(':');
            if (idx > 0)
            {
                var id = rest[..idx];
                var key = rest[(idx + 1)..];
                if (s.Units.TryGetValue(id, out var u) && u.Vars.TryGetValue(key, out var v)) return v;
            }
        }
        else if (writeKey.StartsWith("tilevar:"))
        {
            var rest = writeKey.Substring("tilevar:".Length);
            var idx = rest.IndexOf(':');
            if (idx > 0)
            {
                var pos = rest[..idx];
                var key = rest[(idx + 1)..];
                var comma = pos.IndexOf(',');
                if (comma > 0 && int.TryParse(pos[..comma], out var x) && int.TryParse(pos[(comma + 1)..], out var y))
                {
                    if (x >= 0 && x < s.Tiles.GetLength(0) && y >= 0 && y < s.Tiles.GetLength(1))
                    {
                        var t = s.Tiles[x, y];
                        if (t.Vars.TryGetValue(key, out var v)) return v;
                    }
                }
            }
        }
        else if (writeKey.StartsWith("globvar:"))
        {
            var key = writeKey.Substring("globvar:".Length);
            if (s.Global.Vars.TryGetValue(key, out var v)) return v;
        }
        return null;
    }

    private static bool ReadTagPresence(WorldState s, string writeKey)
    {
        if (writeKey.StartsWith("unitag:"))
        {
            var rest = writeKey.Substring("unitag:".Length);
            var idx = rest.IndexOf(':');
            if (idx > 0)
            {
                var id = rest[..idx];
                var tag = rest[(idx + 1)..];
                return s.Units.TryGetValue(id, out var u) && u.Tags.Contains(tag);
            }
        }
        else if (writeKey.StartsWith("tiltag:"))
        {
            var rest = writeKey.Substring("tiltag:".Length);
            var idx = rest.IndexOf(':');
            if (idx > 0)
            {
                var pos = rest[..idx];
                var tag = rest[(idx + 1)..];
                var comma = pos.IndexOf(',');
                if (comma > 0 && int.TryParse(pos[..comma], out var x) && int.TryParse(pos[(comma + 1)..], out var y))
                {
                    if (x >= 0 && x < s.Tiles.GetLength(0) && y >= 0 && y < s.Tiles.GetLength(1))
                    {
                        return s.Tiles[x, y].Tags.Contains(tag);
                    }
                }
            }
        }
        else if (writeKey.StartsWith("globtag:"))
        {
            var tag = writeKey.Substring("globtag:".Length);
            return s.Global.Tags.Contains(tag);
        }
        return false;
    }

    private static WorldState ApplyVarWrite(WorldState s, string writeKey, object? value)
    {
        if (writeKey.StartsWith("unitvar:"))
        {
            var rest = writeKey.Substring("unitvar:".Length);
            var idx = rest.IndexOf(':');
            if (idx > 0)
            {
                var id = rest[..idx];
                var key = rest[(idx + 1)..];
                // CC immunity: ignore attempts to set *_turns for CC when status_immune_turns > 0
                if ((key == Keys.StunnedTurns || key == Keys.SilencedTurns || key == Keys.RootedTurns)
                    && s.Units.TryGetValue(id, out var uu)
                    && uu.Vars.TryGetValue(Keys.StatusImmuneTurns, out var imv) && imv is int imi && imi > 0)
                {
                    value = 0;
                }
                return WorldStateOps.WithUnit(s, id, u => u with { Vars = u.Vars.SetItem(key, value!) });
            }
        }
        else if (writeKey.StartsWith("tilevar:"))
        {
            var rest = writeKey.Substring("tilevar:".Length);
            var idx = rest.IndexOf(':');
            if (idx > 0)
            {
                var pos = rest[..idx];
                var key = rest[(idx + 1)..];
                var comma = pos.IndexOf(',');
                if (comma > 0 && int.TryParse(pos[..comma], out var x) && int.TryParse(pos[(comma + 1)..], out var y))
                {
                    return WorldStateOps.WithTile(s, new Coord(x, y), t => t with { Vars = t.Vars.SetItem(key, value!) });
                }
            }
        }
        else if (writeKey.StartsWith("globvar:"))
        {
            var key = writeKey.Substring("globvar:".Length);
            return WorldStateOps.WithGlobal(s, g => g with { Vars = g.Vars.SetItem(key, value!) });
        }
        return s;
    }

    private static WorldState ApplyTagWrite(WorldState s, string writeKey, bool present)
    {
        if (writeKey.StartsWith("unitag:"))
        {
            var rest = writeKey.Substring("unitag:".Length);
            var idx = rest.IndexOf(':');
            if (idx > 0)
            {
                var id = rest[..idx];
                var tag = rest[(idx + 1)..];
                // CC immunity: skip adding CC tags while immune
                if (present && (tag == Tags.Stunned || tag == Tags.Silenced || tag == Tags.Rooted)
                    && s.Units.TryGetValue(id, out var uu)
                    && uu.Vars.TryGetValue(Keys.StatusImmuneTurns, out var imv) && imv is int imi && imi > 0)
                {
                    return s; // ignore
                }
                return WorldStateOps.WithUnit(s, id, u => u with { Tags = present ? u.Tags.Add(tag) : u.Tags.Remove(tag) });
            }
        }
        else if (writeKey.StartsWith("tiltag:"))
        {
            var rest = writeKey.Substring("tiltag:".Length);
            var idx = rest.IndexOf(':');
            if (idx > 0)
            {
                var pos = rest[..idx];
                var tag = rest[(idx + 1)..];
                var comma = pos.IndexOf(',');
                if (comma > 0 && int.TryParse(pos[..comma], out var x) && int.TryParse(pos[(comma + 1)..], out var y))
                {
                    return WorldStateOps.WithTile(s, new Coord(x, y), t => t with { Tags = present ? t.Tags.Add(tag) : t.Tags.Remove(tag) });
                }
            }
        }
        else if (writeKey.StartsWith("globtag:"))
        {
            var tag = writeKey.Substring("globtag:".Length);
            return WorldStateOps.WithGlobal(s, g => g with { Tags = present ? g.Tags.Add(tag) : g.Tags.Remove(tag) });
        }
        return s;
    }

    private static void TryPublishSpecializedEvents(EventBus? events, WorldState before, WorldState after, AtomicAction action)
    {
        if (events is null) return;

        switch (action)
        {
            case Damage(var targetId, var amount):
                {
                    var beforeHp = 0;
                    if (before.Units.TryGetValue(targetId, out var bu) && bu.Vars.TryGetValue(Keys.Hp, out var hv)) beforeHp = hv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
                    var afterHp = 0;
                    if (after.Units.TryGetValue(targetId, out var au) && au.Vars.TryGetValue(Keys.Hp, out var hv2)) afterHp = hv2 switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
                    events.Publish(EventTopics.UnitDamaged, new UnitDamagedEvent(targetId, beforeHp, afterHp, Math.Max(0, beforeHp - afterHp)));
                    break;
                }
            case Move(var id, var to):
                {
                    var beforePos = default(Coord);
                    if (before.Units.TryGetValue(id, out var bu) && bu.Vars.TryGetValue(Keys.Pos, out var pv) && pv is Coord bp) beforePos = bp;
                    var afterPos = default(Coord);
                    if (after.Units.TryGetValue(id, out var au) && au.Vars.TryGetValue(Keys.Pos, out var pv2) && pv2 is Coord ap) afterPos = ap;
                    events.Publish(EventTopics.UnitMoved, new UnitMovedEvent(id, beforePos, afterPos));
                    break;
                }
            case AddUnitTag(var id, var tag):
                events.Publish(EventTopics.UnitTagAdded, new UnitTagEvent(id, tag, true));
                break;
            case RemoveUnitTag(var id, var tag):
                events.Publish(EventTopics.UnitTagRemoved, new UnitTagEvent(id, tag, false));
                break;
            case AddTileTag(var pos, var tag):
                events.Publish(EventTopics.TileTagAdded, new TileTagEvent(pos, tag, true));
                break;
            case RemoveTileTag(var pos, var tag):
                events.Publish(EventTopics.TileTagRemoved, new TileTagEvent(pos, tag, false));
                break;
            case SetUnitVar(var id, var key, var value):
                {
                    object? beforeVal = null;
                    if (before.Units.TryGetValue(id, out var bu) && bu.Vars.TryGetValue(key, out var v)) beforeVal = v;
                    events.Publish(EventTopics.UnitVarChanged, new VarChangedEvent("unit", key, beforeVal, value, UnitId: id));
                    break;
                }
            case ModifyUnitVar(var id, var key, _):
                {
                    object? beforeVal = null; object? afterVal = null;
                    if (before.Units.TryGetValue(id, out var bu) && bu.Vars.TryGetValue(key, out var v)) beforeVal = v;
                    if (after.Units.TryGetValue(id, out var au) && au.Vars.TryGetValue(key, out var v2)) afterVal = v2;
                    events.Publish(EventTopics.UnitVarChanged, new VarChangedEvent("unit", key, beforeVal, afterVal, UnitId: id));
                    break;
                }
            case SetTileVar(var pos, var key, var value):
                {
                    object? beforeVal = null;
                    var (x, y) = (pos.X, pos.Y);
                    if (x >= 0 && x < before.Tiles.GetLength(0) && y >= 0 && y < before.Tiles.GetLength(1))
                    {
                        var t = before.Tiles[x, y];
                        if (t.Vars.TryGetValue(key, out var v)) beforeVal = v;
                    }
                    events.Publish(EventTopics.TileVarChanged, new VarChangedEvent("tile", key, beforeVal, value, Pos: pos));
                    break;
                }
            case ModifyTileVar(var pos, var key, _):
                {
                    object? beforeVal = null; object? afterVal = null;
                    var (x, y) = (pos.X, pos.Y);
                    if (x >= 0 && x < before.Tiles.GetLength(0) && y >= 0 && y < before.Tiles.GetLength(1))
                    {
                        var t = before.Tiles[x, y];
                        if (t.Vars.TryGetValue(key, out var v)) beforeVal = v;
                    }
                    if (x >= 0 && x < after.Tiles.GetLength(0) && y >= 0 && y < after.Tiles.GetLength(1))
                    {
                        var t = after.Tiles[x, y];
                        if (t.Vars.TryGetValue(key, out var v2)) afterVal = v2;
                    }
                    events.Publish(EventTopics.TileVarChanged, new VarChangedEvent("tile", key, beforeVal, afterVal, Pos: pos));
                    break;
                }
            case SetGlobalVar(var key, var value):
                {
                    object? beforeVal = null;
                    if (before.Global.Vars.TryGetValue(key, out var v)) beforeVal = v;
                    events.Publish(EventTopics.GlobalVarChanged, new VarChangedEvent("global", key, beforeVal, value));
                    break;
                }
            case AddGlobalTag(var tag):
                events.Publish(EventTopics.GlobalTagAdded, tag);
                break;
            case RemoveGlobalTag(var tag):
                events.Publish(EventTopics.GlobalTagRemoved, tag);
                break;
        }
    }
}


