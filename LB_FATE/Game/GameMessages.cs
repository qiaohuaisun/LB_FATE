using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ETBBS;

namespace LB_FATE;

/// <summary>
/// JSON协议消息构建器
/// </summary>
public static class GameMessages
{
    /// <summary>
    /// 优化的JSON序列化选项（用于网络传输）
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false, // 压缩格式
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // 忽略null值
        NumberHandling = JsonNumberHandling.AllowReadingFromString, // 兼容性
        TypeInfoResolver = new DefaultJsonTypeInfoResolver() // 启用反射序列化（.NET 10需要）
    };

    // ==================== 消息类型定义 ====================

    public record GameStateMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "GAME_STATE";

        [JsonPropertyName("mode")]
        public string Mode { get; init; } = "delta";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("data")]
        public object Data { get; init; } = null!;
    }

    public record CombatEventMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "COMBAT_EVENT";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("data")]
        public CombatEventData Data { get; init; } = null!;
    }

    public record TurnEventMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "TURN_EVENT";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("data")]
        public TurnEventData Data { get; init; } = null!;
    }

    public record SkillUpdateMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "SKILL_UPDATE";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("data")]
        public SkillUpdateData Data { get; init; } = null!;
    }

    public record BossQuoteMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "BOSS_QUOTE";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("data")]
        public BossQuoteData Data { get; init; } = null!;
    }

    public record InputRequestMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "INPUT_REQUEST";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("data")]
        public InputRequestData Data { get; init; } = null!;
    }

    // ==================== 数据类型定义 ====================

    public record UnitData
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("class")]
        public string Class { get; init; } = "";

        [JsonPropertyName("position")]
        public Position Position { get; init; } = new();

        [JsonPropertyName("hp")]
        public int Hp { get; init; }

        [JsonPropertyName("maxHp")]
        public int MaxHp { get; init; }

        [JsonPropertyName("mp")]
        public double Mp { get; init; }

        [JsonPropertyName("maxMp")]
        public double MaxMp { get; init; }

        [JsonPropertyName("tags")]
        public string[] Tags { get; init; } = Array.Empty<string>();

        [JsonPropertyName("isAlly")]
        public bool IsAlly { get; init; }

        [JsonPropertyName("isOffline")]
        public bool IsOffline { get; init; }

        [JsonPropertyName("symbol")]
        public string Symbol { get; init; } = "";
    }

    public record Position
    {
        [JsonPropertyName("x")]
        public int X { get; init; }

        [JsonPropertyName("y")]
        public int Y { get; init; }
    }

    public record GridInfo
    {
        [JsonPropertyName("width")]
        public int Width { get; init; }

        [JsonPropertyName("height")]
        public int Height { get; init; }
    }

    public record CombatEventData
    {
        [JsonPropertyName("eventType")]
        public string EventType { get; init; } = "";

        [JsonPropertyName("actorId")]
        public string? ActorId { get; init; }

        [JsonPropertyName("targetId")]
        public string? TargetId { get; init; }

        [JsonPropertyName("skillName")]
        public string? SkillName { get; init; }

        [JsonPropertyName("damage")]
        public int? Damage { get; init; }

        [JsonPropertyName("healing")]
        public int? Healing { get; init; }

        [JsonPropertyName("effects")]
        public string[]? Effects { get; init; }

        [JsonPropertyName("message")]
        public string Message { get; init; } = "";
    }

    public record TurnEventData
    {
        [JsonPropertyName("eventType")]
        public string EventType { get; init; } = "";

        [JsonPropertyName("playerId")]
        public string? PlayerId { get; init; }

        [JsonPropertyName("day")]
        public int Day { get; init; }

        [JsonPropertyName("phase")]
        public int Phase { get; init; }
    }

    public record SkillUpdateData
    {
        [JsonPropertyName("playerId")]
        public string PlayerId { get; init; } = "";

        [JsonPropertyName("skills")]
        public SkillInfo[] Skills { get; init; } = Array.Empty<SkillInfo>();
    }

    public record SkillInfo
    {
        [JsonPropertyName("index")]
        public int Index { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("mpCost")]
        public double MpCost { get; init; }

        [JsonPropertyName("range")]
        public int Range { get; init; }

        [JsonPropertyName("cooldownMax")]
        public int CooldownMax { get; init; }

        [JsonPropertyName("cooldownLeft")]
        public int CooldownLeft { get; init; }

        [JsonPropertyName("targeting")]
        public string Targeting { get; init; } = "";
    }

    public record BossQuoteData
    {
        [JsonPropertyName("quote")]
        public string Quote { get; init; } = "";

        [JsonPropertyName("eventType")]
        public string EventType { get; init; } = "";

        [JsonPropertyName("context")]
        public string? Context { get; init; }

        [JsonPropertyName("bossId")]
        public string BossId { get; init; } = "";

        [JsonPropertyName("bossName")]
        public string BossName { get; init; } = "";
    }

    public record InputRequestData
    {
        [JsonPropertyName("playerId")]
        public string PlayerId { get; init; } = "";

        [JsonPropertyName("prompt")]
        public string Prompt { get; init; } = "";

        [JsonPropertyName("validCommands")]
        public string[] ValidCommands { get; init; } = Array.Empty<string>();
    }

    // ==================== Data 类型（替代匿名类型，避免 .NET 10 ILLink 裁剪） ====================

    public record FullStateData
    {
        [JsonPropertyName("day")]
        public int Day { get; init; }

        [JsonPropertyName("phase")]
        public int Phase { get; init; }

        [JsonPropertyName("grid")]
        public GridInfo Grid { get; init; } = null!;

        [JsonPropertyName("units")]
        public UnitData[] Units { get; init; } = Array.Empty<UnitData>();

        [JsonPropertyName("highlights")]
        public Position[] Highlights { get; init; } = Array.Empty<Position>();
    }

    public record DeltaStateData
    {
        [JsonPropertyName("day")]
        public int Day { get; init; }

        [JsonPropertyName("phase")]
        public int Phase { get; init; }

        [JsonPropertyName("unitUpdates")]
        public UnitUpdate[] UnitUpdates { get; init; } = Array.Empty<UnitUpdate>();

        [JsonPropertyName("highlights")]
        public Position[] Highlights { get; init; } = Array.Empty<Position>();
    }

    public record UnitUpdate
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("changes")]
        public Dictionary<string, object> Changes { get; init; } = new();
    }

    // ==================== 消息构建方法 ====================

    /// <summary>
    /// 构建完整游戏状态消息（仅在初始化时使用）
    /// </summary>
    public static GameStateMessage BuildFullState(
        int day, int phase, int width, int height,
        Dictionary<string, UnitData> units,
        Position[]? highlights = null)
    {
        return new GameStateMessage
        {
            Mode = "full",
            Data = new FullStateData
            {
                Day = day,
                Phase = phase,
                Grid = new GridInfo { Width = width, Height = height },
                Units = units.Values.ToArray(),
                Highlights = highlights ?? Array.Empty<Position>()
            }
        };
    }

    /// <summary>
    /// 构建增量状态更新消息（默认模式）
    /// </summary>
    public static GameStateMessage BuildDeltaUpdate(
        int day, int phase,
        Dictionary<string, Dictionary<string, object>> unitChanges,
        Position[]? highlights = null)
    {
        return new GameStateMessage
        {
            Mode = "delta",
            Data = new DeltaStateData
            {
                Day = day,
                Phase = phase,
                UnitUpdates = unitChanges.Select(kv => new UnitUpdate
                {
                    Id = kv.Key,
                    Changes = kv.Value
                }).ToArray(),
                Highlights = highlights ?? Array.Empty<Position>()
            }
        };
    }

    /// <summary>
    /// 构建回合事件消息
    /// </summary>
    public static TurnEventMessage BuildTurnEvent(
        string eventType, string? playerId, int day, int phase)
    {
        return new TurnEventMessage
        {
            Data = new TurnEventData
            {
                EventType = eventType,
                PlayerId = playerId,
                Day = day,
                Phase = phase
            }
        };
    }

    /// <summary>
    /// 构建Boss台词消息
    /// </summary>
    public static BossQuoteMessage BuildBossQuote(
        string quote, string eventType, string? context,
        string bossId, string bossName)
    {
        return new BossQuoteMessage
        {
            Data = new BossQuoteData
            {
                Quote = quote,
                EventType = eventType,
                Context = context,
                BossId = bossId,
                BossName = bossName
            }
        };
    }

    /// <summary>
    /// 构建输入请求消息
    /// </summary>
    public static InputRequestMessage BuildInputRequest(string playerId)
    {
        return new InputRequestMessage
        {
            Data = new InputRequestData
            {
                PlayerId = playerId,
                Prompt = "请输入命令",
                ValidCommands = new[] { "move", "attack", "skills", "use", "pass", "info", "help", "quit" }
            }
        };
    }

    /// <summary>
    /// 从游戏世界状态提取UnitData
    /// </summary>
    public static UnitData ExtractUnitData(
        string id, UnitState unit, string className, char symbol,
        bool isAlly, bool isOffline)
    {
        var pos = (Coord)unit.Vars[Keys.Pos];
        var hp = unit.Vars.TryGetValue(Keys.Hp, out var hpVal) ? Convert.ToInt32(hpVal) : 0;
        var maxHp = unit.Vars.TryGetValue(Keys.MaxHp, out var maxHpVal) ? Convert.ToInt32(maxHpVal) : hp;
        var mp = unit.Vars.TryGetValue(Keys.Mp, out var mpVal) ? Convert.ToDouble(mpVal) : 0.0;
        var maxMp = unit.Vars.TryGetValue(Keys.MaxMp, out var maxMpVal) ? Convert.ToDouble(maxMpVal) : mp;

        var tags = unit.Tags.ToArray();

        return new UnitData
        {
            Id = id,
            Name = id,
            Class = className,
            Position = new Position { X = pos.X, Y = pos.Y },
            Hp = hp,
            MaxHp = maxHp,
            Mp = mp,
            MaxMp = maxMp,
            Tags = tags,
            IsAlly = isAlly,
            IsOffline = isOffline,
            Symbol = symbol.ToString()
        };
    }
}
