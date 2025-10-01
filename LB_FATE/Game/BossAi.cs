using ETBBS;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LB_FATE;

internal sealed class BossAiConfig
{
    [JsonPropertyName("rules")] public List<BossAiRule> Rules { get; set; } = new();
    [JsonPropertyName("fallback")] public string? Fallback { get; set; }
}

internal sealed class BossAiRule
{
    [JsonPropertyName("if")] public BossAiIf? If { get; set; }
    [JsonPropertyName("action")] public string? Action { get; set; } // cast|move_to|basic_attack (default: cast)
    [JsonPropertyName("target")] public BossAiTarget? Target { get; set; }
    [JsonPropertyName("skill")] public string? Skill { get; set; } // optional explicit skill name
    [JsonPropertyName("telegraph")] public bool? Telegraph { get; set; } // if true: announce this phase, execute next phase
    [JsonPropertyName("telegraph_message")] public string? TelegraphMessage { get; set; }
    [JsonPropertyName("telegraph_delay")] public int? TelegraphDelay { get; set; } // phases to delay (>=1). default 1
    [JsonPropertyName("chance")] public double? Chance { get; set; } // 0..1 probability to consider this rule
}

internal sealed class BossAiIf
{
    [JsonPropertyName("hp_pct_lte")] public double? HpPctLte { get; set; }
    [JsonPropertyName("phase_in")] public int[]? PhaseIn { get; set; }
    [JsonPropertyName("skill_ready")] public string? SkillReady { get; set; }
    [JsonPropertyName("distance_lte")] public int? DistanceLte { get; set; }
    [JsonPropertyName("min_hits")] public int? MinHits { get; set; }
    [JsonPropertyName("range_of")] public string? RangeOf { get; set; }
    [JsonPropertyName("has_tag")] public string? HasTag { get; set; }
    [JsonPropertyName("target_has_tag")] public string? TargetHasTag { get; set; }
    [JsonPropertyName("min_mp")] public double? MinMp { get; set; }
}

internal sealed class BossAiTarget
{
    [JsonPropertyName("type")] public string Type { get; set; } = "nearest_enemy"; // nearest_enemy|cluster|approach
    [JsonPropertyName("origin")] public string? Origin { get; set; } // caster|point
    [JsonPropertyName("radius")] public int? Radius { get; set; } // for cluster
    [JsonPropertyName("to")] public string? To { get; set; } // for approach: nearest_enemy
    [JsonPropertyName("stop_at_range_of")] public string? StopAtRangeOf { get; set; } // Basic Attack or skill name
    [JsonPropertyName("prefer_tag")] public string? PreferTag { get; set; } // prefer targets with this tag
    [JsonPropertyName("order_var")] public string? OrderVarKey { get; set; } // choose target by var key
    [JsonPropertyName("order_desc")] public bool OrderVarDesc { get; set; }
}

internal static class BossAiLoader
{
    public static BossAiConfig? TryLoad(string? rolesDirArg, RoleDefinition bossRole)
    {
        var roots = new List<string>();

        // Try to add base roles directory
        try
        {
            roots.Add(System.IO.Path.Combine(AppContext.BaseDirectory, "roles"));
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Warning: Failed to add roles directory: {ex.Message}");
        }

        // Add environment variable directory if set
        var envDir = Environment.GetEnvironmentVariable("LB_FATE_ROLES_DIR");
        if (!string.IsNullOrWhiteSpace(envDir)) roots.Add(envDir!);

        // Add argument directory if provided
        if (!string.IsNullOrWhiteSpace(rolesDirArg)) roots.Add(rolesDirArg!);

        // Try to add ai directory
        try
        {
            roots.Add(System.IO.Path.Combine(AppContext.BaseDirectory, "ai"));
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Warning: Failed to add ai directory: {ex.Message}");
        }

        // Try to load role-specific AI config
        foreach (var r in roots)
        {
            var p = System.IO.Path.Combine(r, bossRole.Id + ".ai.json");
            if (System.IO.File.Exists(p))
            {
                var config = LoadFromFile(p);
                if (config != null) return config;
            }
        }

        // Fallback to default AI config
        foreach (var r in roots)
        {
            var p = System.IO.Path.Combine(r, "boss_default.ai.json");
            if (System.IO.File.Exists(p))
            {
                var config = LoadFromFile(p);
                if (config != null) return config;
            }
        }

        return null;
    }

    private static BossAiConfig? LoadFromFile(string path)
    {
        try
        {
            var json = System.IO.File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            return JsonSerializer.Deserialize<BossAiConfig>(json, opts);
        }
        catch (System.IO.IOException ex)
        {
            Console.WriteLine($"Warning: Failed to read AI config file '{path}': {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Warning: Failed to parse AI config file '{path}': {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Unexpected error loading AI config from '{path}': {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }
}
