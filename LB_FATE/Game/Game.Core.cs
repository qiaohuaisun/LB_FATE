using ETBBS;

namespace LB_FATE;

public partial class Game
{
    private int width = 25;
    private int height = 15;
    private readonly Random rng = new();
    private readonly Dictionary<string, string> teamOf = new();
    private readonly Dictionary<string, ClassType> classOf = new();
    private readonly Dictionary<string, char> symbolOf = new();
    private readonly Dictionary<ClassType, ClassDef> defs = new()
    {
        [ClassType.Saber] = new ClassDef(36, 3, 8, 6, 1, 3),
        [ClassType.Rider] = new ClassDef(34, 3, 7, 5, 1, 4),
        [ClassType.Archer] = new ClassDef(28, 4, 6, 4, 3, 2),
        [ClassType.Lancer] = new ClassDef(32, 3, 7, 5, 2, 3),
        [ClassType.Caster] = new ClassDef(26, 6, 5, 3, 2, 2),
        [ClassType.Berserker] = new ClassDef(34, 2, 9, 3, 1, 3),
        [ClassType.Assassin] = new ClassDef(28, 3, 6, 4, 1, 4),
        [ClassType.Beast] = new ClassDef(50, 5, 10, 8, 2, 3),
        [ClassType.Grand] = new ClassDef(60, 8, 12, 10, 3, 2),
    };

    private readonly char[] symbols = new[] { '1', '2', '3', '4', '5', '6', '7' };
    private readonly string[] playerIds;
    private readonly Dictionary<string, IPlayerEndpoint> endpoints = new();

    private WorldState state = null!;
    private EventBus events = new();
    private readonly RoleRegistry registry = new();
    private readonly Dictionary<string, RoleDefinition> roleOf = new();
    private readonly ICooldownStore cooldowns = new InMemoryCooldownStore();
    private readonly string? rolesDirArg;
    // Boss mode support
    private readonly bool bossMode = false;
    private readonly string bossId = "BOSS";
    private string bossName = "BOSS";
    private BossAiConfig? bossAi = null;
    private readonly Dictionary<string, int> phaseDamageTo = new();
    // Track HP thresholds that have been triggered for quotes (unitId -> set of triggered thresholds)
    private readonly Dictionary<string, HashSet<double>> triggeredHpThresholds = new();
    // 日志：public 面向所有玩家；private 面向各自玩家（仅在 debug 模式下追加内部执行细节）
    private readonly List<string> publicLog = new();
    private readonly Dictionary<string, List<string>> privateLog = new();
    private readonly bool debugLogs = Environment.GetEnvironmentVariable("LB_FATE_DEBUG_LOGS") is string v &&
                                       (v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase));
    private readonly bool serverLogs = Environment.GetEnvironmentVariable("LB_FATE_SERVER_LOGS") is string sv &&
                                       (sv.Equals("1") || sv.Equals("true", StringComparison.OrdinalIgnoreCase));
    private HashSet<Coord>? highlightCells = null;
    private char highlightChar = 'o';
    private int lastDay = 1;
    private int lastPhase = 1;

    public Game(string? rolesDir = null, int playerCount = 7, Dictionary<string, IPlayerEndpoint>? endpointMap = null, int? mapWidth = null, int? mapHeight = null)
    {
        rolesDirArg = rolesDir;
        if (mapWidth is int mw && mw > 0) this.width = mw;
        if (mapHeight is int mh && mh > 0) this.height = mh;
        var count = Math.Max(1, Math.Min(playerCount, symbols.Length));
        playerIds = Enumerable.Range(1, count).Select(i => $"P{i}").ToArray();
        if (endpointMap is not null)
            foreach (var kv in endpointMap) endpoints[kv.Key] = kv.Value;
        // Enable boss mode via environment variable LB_FATE_MODE=boss
        try
        {
            var m = Environment.GetEnvironmentVariable("LB_FATE_MODE");
            if (!string.IsNullOrWhiteSpace(m) && m.Equals("boss", StringComparison.OrdinalIgnoreCase))
                bossMode = true;
        }
        catch { }
    }

    // --- Reconnection helpers ---
    public bool HasEndpoint(string pid)
    {
        if (!endpoints.TryGetValue(pid, out var ep)) return false;
        // Check if endpoint is still alive
        if (!ep.IsAlive)
        {
            endpoints.Remove(pid);
            return false;
        }
        return true;
    }

    public void AttachEndpoint(string pid, IPlayerEndpoint ep)
    {
        lock (endpoints)
        {
            endpoints[pid] = ep;
        }
        try
        {
            ep.SendLine($"欢迎 {pid}（已重连）");
            SendBoardTo(pid, lastDay, lastPhase);
            foreach (var kv in endpoints)
                if (kv.Key != pid)
                    kv.Value.SendLine($"玩家重连：{pid}");
        }
        catch { }
    }

    // --- Public accessors for AutoComplete ---
    public WorldState GetState() => state;
    public int GetIntPublic(string id, string key, int def = 0) => GetInt(id, key, def);
    public RoleDefinition? GetRoleOf(string pid) => roleOf.TryGetValue(pid, out var r) ? r : null;

    // --- Server-side logging helpers ---
    private void ServerLog(string msg)
    {
        if (!serverLogs) return;
        try { Console.WriteLine(msg); } catch { }
        if (endpoints.Count > 0)
        {
            foreach (var ep in endpoints.Values)
            {
                try { ep.SendLine($"[Srv] {msg}"); } catch { }
            }
        }
        try { publicLog.Add(msg); } catch { }
    }

    private void SetupEventLogging()
    {
        if (!serverLogs) return;
        try
        {
            events.Subscribe(EventTopics.ActionExecuting, o =>
            {
                if (o is ActionExecutingEvent a)
                    ServerLog($"ActionExecuting: {a.Action}");
            });
            events.Subscribe(EventTopics.ActionExecuted, o =>
            {
                if (o is ActionExecutedEvent a)
                    ServerLog($"ActionExecuted: {a.Action}");
            });
            events.Subscribe(EventTopics.ValidationFailed, o =>
            {
                if (o is ValidationFailedEvent v)
                    ServerLog($"ValidationFailed: {v.Reason}");
            });
            events.Subscribe(EventTopics.ConflictDetected, o =>
            {
                if (o is ConflictDetectedEvent c)
                    ServerLog($"Conflict: #{c.IndexA} vs #{c.IndexB} ({c.A.GetType().Name} vs {c.B.GetType().Name})");
            });
            events.Subscribe(EventTopics.UnitDamaged, o =>
            {
                if (o is UnitDamagedEvent e)
                {
                    ServerLog($"Damaged: {e.UnitId} -{e.Amount} HP {e.BeforeHp}->{e.AfterHp}");
                    try
                    {
                        if (!phaseDamageTo.ContainsKey(e.UnitId)) phaseDamageTo[e.UnitId] = 0;
                        phaseDamageTo[e.UnitId] += Math.Max(0, e.Amount);
                    }
                    catch { }
                }
            });
            events.Subscribe(EventTopics.UnitDied, o =>
            {
                if (o is UnitDiedEvent e)
                {
                    var msg = $"【{e.UnitId} 阵亡】";
                    ServerLog(msg);
                    AppendPublic(new[] { msg });
                }
            });
            events.Subscribe(EventTopics.UnitMoved, o =>
            {
                if (o is UnitMovedEvent e)
                    ServerLog($"Moved: {e.UnitId} {e.Before} -> {e.After}");
            });
            events.Subscribe(EventTopics.UnitTagAdded, o => { if (o is UnitTagEvent e && e.Added) ServerLog($"UnitTag+ {e.UnitId}:{e.Tag}"); });
            events.Subscribe(EventTopics.UnitTagRemoved, o => { if (o is UnitTagEvent e && !e.Added) ServerLog($"UnitTag- {e.UnitId}:{e.Tag}"); });
            events.Subscribe(EventTopics.TileTagAdded, o => { if (o is TileTagEvent e && e.Added) ServerLog($"TileTag+ {e.Pos}:{e.Tag}"); });
            events.Subscribe(EventTopics.TileTagRemoved, o => { if (o is TileTagEvent e && !e.Added) ServerLog($"TileTag- {e.Pos}:{e.Tag}"); });
            events.Subscribe(EventTopics.GlobalVarChanged, o =>
            {
                if (o is VarChangedEvent e)
                {
                    var before = e.Before is null ? "<null>" : e.Before.ToString();
                    var after = e.After is null ? "<null>" : e.After.ToString();
                    ServerLog($"GlobalVar: {e.Key} {before} -> {after}");
                }
            });
            events.Subscribe(EventTopics.GlobalTagAdded, o => { if (o is string tag) ServerLog($"GlobalTag+ {tag}"); });
            events.Subscribe(EventTopics.GlobalTagRemoved, o => { if (o is string tag) ServerLog($"GlobalTag- {tag}"); });
        }
        catch { }
    }
}
