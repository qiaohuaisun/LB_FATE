# Replay JSON Serialization

`ReplayJson` provides a simple way to serialize/deserialize complete game replays.

File: `ETBBS/Systems/Replay.Serialization.cs`

## API

- `string ReplayJson.Serialize(ReplayRecord replay)`
- `void ReplayJson.SerializeTo(Stream stream, ReplayRecord replay)`
- `ReplayRecord ReplayJson.Deserialize(string json)`
- `ReplayRecord ReplayJson.DeserializeFrom(Stream stream)`

## Format

JSON object with fields:

- `initial` → world state at start
- `steps` → array of action batches; actions are stored as strings (for now)
- `final` → world state at end
- `logs` → per-batch logs

World state includes global, tiles (as a 2D jagged array of `{ vars, tags }`), and units (map of id → `{ vars, tags }`). Coordinates are serialized as `[x,y]` arrays.

> Note: Actions are stored as strings for traceability; executing them back requires a richer encoding and is left for future work.

## Example

```csharp
var replay = new ReplaySystem().Record(WorldState.CreateEmpty(8, 8), new List<AtomicAction[]> {
    new [] { new SetGlobalVar("start", 1) }
});
var json = ReplayJson.Serialize(replay);
File.WriteAllText("replay.json", json);
var roundtrip = ReplayJson.Deserialize(json);
```

### Sample JSON

```json
{
  "initial": {
    "global": { "turn": 0, "vars": {}, "tags": [] },
    "tiles": [[{ "vars": {}, "tags": [] }]],
    "units": {}
  },
  "steps": [["SetGlobalVar(start,1)"]],
  "final": { "global": { "turn": 0, "vars": { "start": 1 }, "tags": [] }, "tiles": [[{ "vars": {}, "tags": [] }]], "units": {} },
  "logs": [["Do: SetGlobalVar(start,1)"]]
}
```

## 中文说明

- `ReplayJson` 用于把完整回放序列（初始状态/步骤/最终状态/日志）序列化为 JSON，或从 JSON 还原。
- 世界状态包含：全局（turn/vars/tags）、地图格（二维数组，含 vars/tags）、单位（id → vars/tags）。坐标类型 `Coord` 序列化为 `[x,y]` 数组。
- 注意：当前 `steps` 中的动作以字符串形式保存，便于可视化与调试；若要可执行回放，需要后来扩展更丰富的编码（例如动作类型 + 参数对象）。
