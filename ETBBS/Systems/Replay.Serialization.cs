using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ETBBS;

public static class ReplayJson
{
    public static string Serialize(ReplayRecord replay)
    {
        var dto = ToDto(replay);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static void SerializeTo(Stream stream, ReplayRecord replay)
    {
        var dto = ToDto(replay);
        JsonSerializer.Serialize(stream, dto, JsonOptions);
    }

    public static ReplayRecord Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<ReplayDto>(json, JsonOptions) ?? throw new InvalidOperationException("Invalid replay JSON");
        return FromDto(dto);
    }

    public static ReplayRecord DeserializeFrom(Stream stream)
    {
        var dto = JsonSerializer.Deserialize<ReplayDto>(stream, JsonOptions) ?? throw new InvalidOperationException("Invalid replay JSON");
        return FromDto(dto);
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new CoordJsonConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    };

    private static ReplayDto ToDto(ReplayRecord r)
    {
        return new ReplayDto
        {
            Initial = WorldToDto(r.Initial),
            Steps = r.Steps.Select(batch => batch.Select(ActionToString).ToArray()).ToArray(),
            Final = WorldToDto(r.Final),
            Logs = r.Logs.Select(list => list.ToArray()).ToArray()
        };
    }

    private static ReplayRecord FromDto(ReplayDto dto)
    {
        var initial = WorldFromDto(dto.Initial);
        var final = WorldFromDto(dto.Final);
        var steps = dto.Steps.Select(batch => batch.Select(ActionFromString).ToArray()!).ToList();
        var logs = dto.Logs.Select(l => (IReadOnlyList<string>)l.ToList()).ToList();
        return new ReplayRecord(initial, steps, final, logs);
    }

    private static string ActionToString(AtomicAction action)
        => action.ToString() ?? action.GetType().Name;

    private static AtomicAction ActionFromString(string text)
    {
        // For now, do not attempt to reconstruct executable actions; placeholder only.
        // Replays can be used for visualization; executing requires richer encoding.
        return new SetGlobalVar("_raw_action", text);
    }

    private static WorldDto WorldToDto(WorldState s)
    {
        return new WorldDto
        {
            Global = new GlobalDto { Turn = s.Global.Turn, Vars = s.Global.Vars.ToDictionary(kv => kv.Key, kv => kv.Value), Tags = s.Global.Tags.ToArray() },
            Tiles = TilesToDto(s.Tiles),
            Units = s.Units.ToDictionary(kv => kv.Key, kv => new UnitDto { Vars = kv.Value.Vars.ToDictionary(kv => kv.Key, kv => kv.Value), Tags = kv.Value.Tags.ToArray() })
        };
    }

    private static WorldState WorldFromDto(WorldDto dto)
    {
        var tiles = TilesFromDto(dto.Tiles);
        var units = dto.Units.ToDictionary(kv => kv.Key, kv => new UnitState(kv.Value.Vars.ToImmutableDictionary(), kv.Value.Tags.ToImmutableHashSet()));
        var global = new GlobalState(dto.Global.Turn, dto.Global.Vars.ToImmutableDictionary(), dto.Global.Tags.ToImmutableHashSet());
        return new WorldState(global, tiles, units.ToImmutableDictionary());
    }

    private static TileDto[][] TilesToDto(TileState[,] tiles)
    {
        int w = tiles.GetLength(0), h = tiles.GetLength(1);
        var rows = new TileDto[w][];
        for (int x = 0; x < w; x++)
        {
            rows[x] = new TileDto[h];
            for (int y = 0; y < h; y++)
            {
                var t = tiles[x, y];
                rows[x][y] = new TileDto { Vars = t.Vars.ToDictionary(kv => kv.Key, kv => kv.Value), Tags = t.Tags.ToArray() };
            }
        }
        return rows;
    }

    private static TileState[,] TilesFromDto(TileDto[][] rows)
    {
        int w = rows.Length; int h = rows.Length > 0 ? rows[0].Length : 0;
        var tiles = new TileState[w, h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var t = rows[x][y];
                tiles[x, y] = new TileState(t.Vars.ToImmutableDictionary(), t.Tags.ToImmutableHashSet());
            }
        }
        return tiles;
    }

    private sealed class CoordJsonConverter : JsonConverter<Coord>
    {
        public override Coord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Read(); var x = reader.GetInt32();
                reader.Read(); var y = reader.GetInt32();
                reader.Read(); // EndArray
                return new Coord(x, y);
            }
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                int x = 0, y = 0; reader.Read();
                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    var name = reader.GetString(); reader.Read();
                    if (string.Equals(name, "x", StringComparison.OrdinalIgnoreCase)) x = reader.GetInt32();
                    else if (string.Equals(name, "y", StringComparison.OrdinalIgnoreCase)) y = reader.GetInt32();
                    reader.Read();
                }
                return new Coord(x, y);
            }
            throw new JsonException("Invalid Coord");
        }
        public override void Write(Utf8JsonWriter writer, Coord value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteEndArray();
        }
    }

    private sealed class ReplayDto
    {
        public required WorldDto Initial { get; set; }
        public required string[][] Steps { get; set; }
        public required WorldDto Final { get; set; }
        public required string[][] Logs { get; set; }
    }

    private sealed class WorldDto
    {
        public required GlobalDto Global { get; set; }
        public required TileDto[][] Tiles { get; set; }
        public required Dictionary<string, UnitDto> Units { get; set; } = new();
    }
    private sealed class GlobalDto
    {
        public required int Turn { get; set; }
        public required Dictionary<string, object> Vars { get; set; } = new();
        public required string[] Tags { get; set; } = Array.Empty<string>();
    }
    private sealed class TileDto
    {
        public required Dictionary<string, object> Vars { get; set; } = new();
        public required string[] Tags { get; set; } = Array.Empty<string>();
    }
    private sealed class UnitDto
    {
        public required Dictionary<string, object> Vars { get; set; } = new();
        public required string[] Tags { get; set; } = Array.Empty<string>();
    }
}

