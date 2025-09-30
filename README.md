# ETBBS + LB_FATE

Language: English | 中文见 README.zh-CN.md

Enhanced Turn‑Based Behavior System (ETBBS) and a sample console game (LB_FATE).

This repository contains a reusable .NET 8 core for grid/turn‑based games and a
playable sample that demonstrates the system end‑to‑end, plus a VS Code
extension and example role scripts.

## Overview

- Core library `ETBBS` implements immutable world state, atomic actions,
  skill composition/execution, an events bus, and a text DSL (LBR) for defining
  roles and skills.
- Sample game `LB_FATE` runs a 2D grid, loads `.lbr` roles, and supports local
  and TCP multiplayer. Entry point is `LB_FATE/Program.Main.cs`.
- **Avalonia GUI Client** `LB_FATE.AvaloniaClient` provides a modern cross-platform
  graphical interface with interactive game board, real-time unit status, and mouse controls.
- VS Code extension under `vscode-extension/` adds syntax highlighting,
  snippets, completions, formatting, and basic diagnostics for `.lbr` files.

## Repo Structure

- `ETBBS/` — Core library
  - State/keys/context: `Core/*.cs`
  - Atomic actions: `Actions/*.cs`
  - Skills/execution/validation: `Skills/*.cs`
  - DSL for roles/skills: `DSL/*.cs`
  - Systems: turn advancement, replay, events: `Systems/*.cs`
- `LB_FATE/` — Sample console game (local or host/client)
  - Entry: `Program.Main.cs`
  - Game loop/UI: `Game/*.cs`
  - Networking: `Net.cs`
  - Domain model: `Domain.cs`
- `LB_FATE.AvaloniaClient/` — Cross-platform GUI client
  - Modern Avalonia UI interface
  - MVVM architecture
  - Interactive game board with mouse controls
  - Real-time unit status and combat log
  - See `LB_FATE.AvaloniaClient/README.md` for details
- `ETBBS.LbrValidator/` — Command-line LBR syntax validator
  - Batch validation of `.lbr` files
  - Recursive directory scanning
  - Detailed error reporting and statistics
  - CI/CD integration support
  - See `ETBBS.LbrValidator/README.md` for details
- `roles/` — Example `.lbr` roles and skills
- `docs/` — DSL docs (Chinese), plus this README references
- `vscode-extension/` — VS Code extension sources and packaged `.vsix`
- `publish/win-x64/` — Prebuilt Windows binaries and helper scripts

## Build

- Requirements: .NET SDK 8.0+
- Build solution:
  - `dotnet build ETBBS.sln -c Release`

## Run (Sample Game)

### Avalonia GUI Client (Recommended)

**Modern cross-platform graphical interface:**

1. **Start the server** (using launcher scripts or command line)
2. **Launch the GUI client**:
   ```bash
   dotnet run --project LB_FATE.AvaloniaClient/LB_FATE.AvaloniaClient.csproj
   ```
3. **Connect**: Enter server host/port on Connection tab and click "Connect to Server"
4. **Play**: Switch to Game tab and use mouse or commands to play

**Features**:
- Interactive game board with mouse controls (left-click to move, right-click to attack)
- Real-time unit status with HP/MP bars
- Skills panel with cooldown tracking
- Combat log
- Command input with autocomplete hints

See `LB_FATE.AvaloniaClient/README.md` for detailed usage.

### Console Client (Traditional)

#### Quick Start (Windows)

Use the convenient launcher scripts in `publish/`:
- **Server**: `runServer.cmd` - Interactive log level selection
- **Client**: `runClient.cmd` - Interactive log level selection
- **Debug Server**: `runServer-logs.cmd` - Verbose logs + performance tracking
- **Debug Client**: `runClient-logs.cmd` - Verbose logs + auto-reconnect

See `publish/README_LAUNCHER.md` for detailed usage guide.

#### Command Line

- Local single‑console (7 players simulated):
  - `dotnet run --project LB_FATE/LB_FATE.csproj`
- Use custom roles folder:
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --roles roles`
- Host server (TCP):
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --host --players 4 --port 35500 [--roles PATH] [--size WxH] [--mode ffa|boss]`
- Client:
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --client 127.0.0.1:35500`

### Logging Options

Control log verbosity with environment variables or command-line flags:

**Environment Variable** (all platforms):
```bash
# Windows CMD
set LB_FATE_LOG_LEVEL=Debug

# Windows PowerShell
$env:LB_FATE_LOG_LEVEL="Debug"

# Linux/macOS
export LB_FATE_LOG_LEVEL=Debug
```

**Command-line Flags**:
- `--verbose` / `-v` - Verbose logging (maximum detail)
- `--debug` / `-d` - Debug logging (development)
- `--perf` - Enable performance tracking

**Log Levels** (in order of verbosity):
- `Verbose` - All logs including trace
- `Debug` - Debug information
- `Information` - Standard logs (default)
- `Warning` - Warnings and errors only
- `Error` - Errors only
- `Fatal` - Fatal errors only

**Examples**:
```bash
# Development with debug logs
dotnet run --project LB_FATE/LB_FATE.csproj -- --host --debug --perf

# Production with minimal logs
LB_FATE_LOG_LEVEL=Warning dotnet run --project LB_FATE/LB_FATE.csproj -- --host

# Client with verbose logs for troubleshooting
dotnet run --project LB_FATE/LB_FATE.csproj -- --client 127.0.0.1:35500 --verbose
```

### Other Environment Variables

- **Roles Directory**: `LB_FATE_ROLES_DIR=<dir>` (recursively loaded)
- **Game Mode**: `LB_FATE_MODE=boss` (or `ffa`). Command line `--mode` takes precedence.

### Modes

- `ffa`: default free‑for‑all.
- `boss`: 7 players cooperate versus an AI‑controlled Boss. The Boss is randomly chosen among roles with beast/grand hints (tag, class, or id). Falls back to high‑HP roles or built‑in `boss_beast`.

### Boss AI Rule Script (optional)

- Location: prefer `roles/<boss_id>.ai.json`, fallback to `ai/boss_default.ai.json`.
- Shape (JSON):
  - `rules`: array, matched from top to bottom; first match executes.
    - `if`: all optional conditions
      - `hp_pct_lte`: double in [0,1].
      - `phase_in`: int[] phases where it fires.
      - `skill_ready`: string skill name (checks MP/CD; range ensured by target selection).
      - `distance_lte`: int; nearest‑enemy distance ≤ N.
      - `min_hits` + `range_of`: estimate hits for cluster/aoe.
      - `has_tag`: string; caster has the tag.
    - `target`:
      - `type`: `nearest_enemy` | `cluster` | `approach`
      - `radius`: int; Chebyshev radius for `cluster`.
      - `stop_at_range_of`: string; for `approach`, stop within range of this skill/basic.
    - `action`: `cast` | `move_to` | `basic_attack` (default `cast`).
    - `skill`: string; explicit skill name (optional if using `if.skill_ready`).
    - `telegraph`: bool; this phase only announces, no cast.
    - `telegraph_delay`: int; delay in phases (≥1, default 1; can span days).
    - `telegraph_message`: string; message announced via AppendPublic.
  - `fallback`: e.g., `basic_attack`.

- Fallbacks at execution time (when telegraphed target is invalid):
  - Tile skills: within caster `range`, choose the tile closest to the nearest enemy.
  - Unit skills: for line‑aoe skills, scan all enemies and pick the target that maximizes “hits along the greedy path with radius R” (ties break by proximity); otherwise pick the nearest enemy.
  - Both telegraph announce and final cast emit public messages.

- Example (from `roles/boss_beast.ai.json`):

```
{
  "rules": [
    {
      "if": { "skill_ready": "Mass Sweep", "min_hits": 2, "range_of": "Mass Sweep" },
      "target": { "type": "cluster", "origin": "caster", "radius": 2 },
      "telegraph": true,
      "telegraph_delay": 1,
      "telegraph_message": "Beast preparing Mass Sweep: radius 2 next phase"
    }
  ],
  "fallback": "basic_attack"
}
```

Notes
- `LB_FATE/Program.cs` (older single‑file prototype) has been removed to avoid confusion.
  The entry point is `LB_FATE/Program.Main.cs` and the partial `Game` class under `LB_FATE/Game/`.

## Controls

Phases 1 & 5: all commands; Phases 2-4: move/pass/skills/info/help/hint.

- `move x y` — Move to a reachable tile (clear path ≤ speed). Cost: 0.5 MP per move.
- `attack P#` — Basic attack the target player id. Cost: 0.5 MP.

## LBR DSL (Roles & Skills)

- Role files: `role "Name" id "id" { description/vars/tags/skills }`
- Skills: imperative mini‑DSL with meta and statements:
  - Meta: `cost mp N; range N; cooldown N; targeting any|enemies|allies|self|tile; min_range N; sealed_until day D [phase P]; [ends_turn;]`
    - Legacy: `sealed_until T;` still works but uses 0-based internal turn index.
  - Control: `if ... then ... [else ...]`, `repeat N times ...`, `parallel { ... }`,
    `for each <selector> [in parallel] do { ... }`, `chance P% then ... [else ...]`
  - Actions: `deal [physical|magic] N ... [ignore ...%]`, `line ... length L [radius R]`,
    `heal`, `move`, `dash towards`, `add/remove tag`,
    `set unit(...) var "k" = value`, `set tile(...) var "k" = value`, `set global var "k" = value`,
    `consume mp = N`
  - Value refs: `var "k" of caster|target|it|unit id "..."`, with simple `+`/`-` arithmetic

See `docs/lbr.zh-CN.md` for the full guide.

## LBR Validator Tool

Validate `.lbr` file syntax before runtime:

```bash
# Validate all .lbr files in roles/ directory
dotnet run --project ETBBS.LbrValidator -- roles -v

# Recursive scan with details
dotnet run --project ETBBS.LbrValidator -- roles -r -d

# Quiet mode (summary only)
dotnet run --project ETBBS.LbrValidator -- roles -q
```

**Features**:
- Batch validation of multiple files
- Recursive directory scanning
- Detailed error reporting with line numbers
- Statistics and colored output
- Exit codes for CI/CD integration (0 = success, 1 = errors)

See `ETBBS.LbrValidator/README.md` for complete documentation.

## Launcher Scripts

### Windows

Use the convenient scripts in `publish/` directory:

| Script | Purpose | Log Level |
|--------|---------|-----------|
| `runServer.cmd` | Standard server | Interactive selection (Information/Debug/Verbose/Warning) |
| `runServer-logs.cmd` | Debug server | Verbose (fixed) + performance tracking |
| `runClient.cmd` | Standard client | Interactive selection (Information/Debug/Warning) |
| `runClient-logs.cmd` | Debug client | Verbose (fixed) + auto-reconnect |

**Interactive Selection Example**:
```
> runServer.cmd

Log Level Options:
  1. Information (Production, default)
  2. Debug (Development)
  3. Verbose (Detailed debugging)
  4. Warning (Minimal logs)

Select log level [1-4, default 1]: 2

Selected log level: Debug
```

For detailed usage guide, see `publish/README_LAUNCHER.md`.

### Configuration

- **Roles**: Loaded from `roles/` folder, `LB_FATE_ROLES_DIR`, or `--roles` argument
- **Log Files**: Generated in `logs/` directory with daily rotation
- **Performance Logs**: Available when using `--perf` flag or `-logs.cmd` scripts

## Encoding Note (Windows)

- The console app now forces UTF-8 I/O so Chinese text renders correctly.
- If you still see garbled characters, use Windows Terminal, ensure the font supports CJK, or run `chcp 65001` before starting.


