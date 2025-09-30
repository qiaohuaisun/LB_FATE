# ETBBS + LB_FATE

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.txt)

**Language**: English | [中文](README.zh-CN.md)

A reusable .NET 8 framework for grid-based turn-based strategy games, featuring an immutable state engine, text-based skill DSL, and a playable sample game with GUI client.

---

## 📋 Table of Contents

- [Features](#-features)
- [Quick Start](#-quick-start)
- [Architecture](#-architecture)
- [Repository Structure](#-repository-structure)
- [Getting Started](#-getting-started)
  - [Prerequisites](#prerequisites)
  - [Building](#building)
  - [Running the Game](#running-the-game)
- [Clients](#-clients)
  - [Avalonia GUI Client](#avalonia-gui-client-recommended)
  - [Console Client](#console-client)
- [Development Tools](#-development-tools)
  - [LBR Validator](#lbr-validator)
  - [VS Code Extension](#vs-code-extension)
- [Game Modes](#-game-modes)
- [LBR DSL](#-lbr-dsl-roles--skills)
- [Configuration](#-configuration)
- [Documentation](#-documentation)
- [License](#-license)

---

## ✨ Features

### Core Framework (ETBBS)
- **Immutable World State**: Predictable, testable game logic with atomic actions
- **Text-based Skill DSL**: Define roles and skills using `.lbr` files
- **Event Bus System**: Reactive game events and state changes
- **Turn-based Execution**: Phase-based turn system with validation
- **Flexible Targeting**: Support for single-target, AoE, line attacks, and selectors

### Sample Game (LB_FATE)
- **2D Grid Combat**: Tactical turn-based combat on customizable grids
- **TCP Multiplayer**: Host/client architecture for network play
- **Boss Mode**: Cooperative 7v1 with AI-controlled boss using script-based decision trees
- **Free-for-All Mode**: Traditional battle royale
- **Modern GUI**: Cross-platform Avalonia UI with mouse controls
- **Traditional Console**: Text-based interface for terminal enthusiasts

### Developer Experience
- **Syntax Validator**: CLI tool to validate `.lbr` files before runtime
- **VS Code Extension**: Syntax highlighting, completions, snippets, and diagnostics
- **Comprehensive Logging**: Multiple log levels with performance tracking
- **Hot-reloadable Roles**: Load custom roles from directories

---

## 🚀 Quick Start

### GUI Client (Recommended)

1. **Start the server**:
   ```bash
   cd publish
   runServer.cmd  # Windows
   # Or: dotnet run --project LB_FATE -- --host --players 4
   ```

2. **Launch the GUI client**:
   ```bash
   dotnet run --project LB_FATE.AvaloniaClient
   ```

3. **Connect and play**:
   - Enter server address (e.g., `127.0.0.1:35500`)
   - Use mouse to control units (left-click to move, right-click to attack)

### Console Client

```bash
# Local single-player (simulates 7 AI players)
dotnet run --project LB_FATE

# Host multiplayer server
dotnet run --project LB_FATE -- --host --players 4 --port 35500

# Connect as client
dotnet run --project LB_FATE -- --client 127.0.0.1:35500
```

---

## 🏗 Architecture

```
┌──────────────────────────────────────────────────────┐
│                   LB_FATE (Game)                     │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐    │
│  │ Console UI │  │ Avalonia   │  │  TCP Net   │    │
│  │            │  │ GUI Client │  │ Host/Client│    │
│  └──────┬─────┘  └──────┬─────┘  └──────┬─────┘    │
│         └────────────────┴────────────────┘          │
└───────────────────┬──────────────────────────────────┘
                    │
┌───────────────────▼──────────────────────────────────┐
│              ETBBS Core Library                      │
│  ┌─────────┐  ┌──────────┐  ┌──────────┐           │
│  │  World  │  │ Actions  │  │  Skills  │           │
│  │  State  │◄─┤ (Atomic) │◄─┤   DSL    │           │
│  └────┬────┘  └──────────┘  └──────────┘           │
│       │                                              │
│  ┌────▼────┐  ┌──────────┐  ┌──────────┐           │
│  │  Turn   │  │  Events  │  │LBR Parser│           │
│  │ System  │  │   Bus    │  │          │           │
│  └─────────┘  └──────────┘  └──────────┘           │
└──────────────────────────────────────────────────────┘
```

**Key Concepts**:
- **Immutable State**: Every action produces a new world state
- **Atomic Actions**: Composable, validated actions (damage, heal, move, etc.)
- **Skill Scripts**: Text-based DSL compiled to action sequences
- **Event-Driven**: State changes emit events for UI/logging

---

## 📁 Repository Structure

```
ETBBS/
├── ETBBS/                      # Core library
│   ├── Core/                   # World state, keys, context
│   ├── Actions/                # Atomic actions (damage, heal, move)
│   ├── Skills/                 # Skill execution and validation
│   ├── DSL/                    # LBR parser and compiler
│   └── Systems/                # Turn system, events, replay
│
├── LB_FATE/                    # Sample console game
│   ├── Game/                   # Game loop, initialization, turn logic
│   ├── Program.Main.cs         # Entry point
│   └── Net.cs                  # TCP networking
│
├── LB_FATE.AvaloniaClient/     # Modern GUI client
│   ├── ViewModels/             # MVVM view models
│   ├── Views/                  # XAML views
│   ├── Controls/               # Custom game board control
│   ├── Services/               # Network client, state parser
│   └── README.md               # GUI client documentation
│
├── ETBBS.LbrValidator/         # CLI validation tool
│   ├── Program.cs              # Validator logic
│   └── README.md               # Validator documentation
│
├── ETBBS.Tests/                # Unit tests
├── LB_FATE.Tests/              # Integration tests
│
├── docs/                       # Documentation
│   ├── lbr.zh-CN.md            # LBR DSL guide (Chinese)
│   └── lbr.en.md               # LBR DSL guide (English)
│
├── publish/                    # Distribution
│   ├── roles/                  # Example role files (.lbr)
│   ├── runServer.cmd           # Server launcher
│   ├── runClient.cmd           # Client launcher
│   └── README_LAUNCHER.md      # Launcher guide
│
└── vscode-extension/           # VS Code extension
    └── etbbs-lbr-tools-*.vsix  # Installable extension
```

---

## 🎯 Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- (Optional) [VS Code](https://code.visualstudio.com/) for `.lbr` editing

### Building

```bash
# Clone the repository
git clone https://github.com/qiaohuaisun/LB_FATE.git
cd ETBBS

# Restore and build
dotnet restore
dotnet build ETBBS.sln -c Release

# Run tests
dotnet test
```

### Running the Game

#### Local Mode (Single-Player)
```bash
dotnet run --project LB_FATE
```
Simulates 7 AI-controlled players in a free-for-all battle.

#### Multiplayer Mode

**Host a server**:
```bash
dotnet run --project LB_FATE -- --host --players 4 --port 35500 --mode ffa
```

**Connect clients**:
```bash
dotnet run --project LB_FATE -- --client 127.0.0.1:35500
```

#### Boss Mode
```bash
dotnet run --project LB_FATE -- --host --mode boss --players 7
```
7 players cooperate against an AI boss with scripted behavior.

---

## 🖥 Clients

### Avalonia GUI Client (Recommended)

**Modern cross-platform interface with:**
- Interactive game board (drag-and-drop, click to move/attack)
- Real-time HP/MP bars and status effects
- Skills panel with cooldown tracking
- Combat log with event history
- Command console for advanced users

**Launch**:
```bash
dotnet run --project LB_FATE.AvaloniaClient
```

**Controls**:
- **Left-click**: Select unit / Move to tile
- **Right-click**: Attack target
- **Mouse wheel**: Zoom (planned)
- **Command bar**: Type commands directly

📖 **Full Guide**: [LB_FATE.AvaloniaClient/README.md](LB_FATE.AvaloniaClient/README.md)

---

### Console Client

**Text-based interface for:**
- Terminal enthusiasts
- Low-resource environments
- Scripting and automation

**Launcher Scripts** (Windows):

| Script | Purpose | Logs |
|--------|---------|------|
| `publish/runServer.cmd` | Standard server | Interactive selection |
| `publish/runServer-logs.cmd` | Debug server | Verbose + perf tracking |
| `publish/runClient.cmd` | Standard client | Interactive selection |
| `publish/runClient-logs.cmd` | Debug client | Verbose + auto-reconnect |

**Commands** (Phases 2-4):
- `move x y` - Move to (x, y). Cost: 0.5 MP
- `attack P#` - Attack player. Cost: 0.5 MP
- `cast <skill> [target]` - Use skill
- `pass` - End turn
- `info` - Show unit details
- `help` - List available commands

---

## 🛠 Development Tools

### LBR Validator

Validate `.lbr` files before runtime to catch syntax errors early.

**Usage**:
```bash
# Validate all files in a directory
dotnet run --project ETBBS.LbrValidator -- publish/roles -v

# Recursive scan with details
dotnet run --project ETBBS.LbrValidator -- publish/roles -r -d

# Quiet mode (summary only)
dotnet run --project ETBBS.LbrValidator -- publish/roles -q
```

**Features**:
- ✅ Batch validation of multiple files
- ✅ Recursive directory scanning
- ✅ Colored terminal output (✓/✗)
- ✅ Detailed error messages with line numbers
- ✅ CI/CD friendly (exit codes: 0 = success, 1 = errors)

**Example Output**:
```
═══════════════════════════════════════════════════════
  ETBBS LBR Validator - Role File Syntax Checker
═══════════════════════════════════════════════════════

Found 9 .lbr file(s) to validate

Validating: artoria.lbr ... ✓ OK
Validating: beast_florence.lbr ... ✓ OK
...

✓ ALL FILES PASSED VALIDATION
```

📖 **Full Guide**: [ETBBS.LbrValidator/README.md](ETBBS.LbrValidator/README.md)

---

## 🎮 Game Modes

### Free-for-All (FFA)
- **Default mode**: 7 players compete for survival
- **Victory**: Last player standing
- **Grid**: 10x10 by default (customizable with `--size WxH`)

### Boss Mode
- **Cooperative**: 7 players vs 1 AI-controlled boss
- **Boss Selection**: Roles with `beast`/`grand` tags (e.g., `beast_florence`)
- **AI Behavior**: Defined in `roles/<boss_id>.ai.json`
- **Victory**: Defeat the boss within turn limit

**Boss AI Script Example**:
```json
{
  "rules": [
    {
      "if": {
        "hp_pct_lte": 0.5,
        "skill_ready": "Berserk Mode",
        "phase_in": [3, 4]
      },
      "target": { "type": "nearest_enemy" },
      "action": "cast",
      "skill": "Berserk Mode"
    },
    {
      "if": { "distance_lte": 2, "skill_ready": "Mass Sweep" },
      "target": { "type": "cluster", "radius": 2 },
      "telegraph": true,
      "telegraph_delay": 1,
      "telegraph_message": "Boss charging Mass Sweep!"
    }
  ],
  "fallback": "basic_attack"
}
```

**Telegraph System**: Boss announces powerful attacks one phase in advance, giving players time to react.

---

## 📝 LBR DSL (Roles & Skills)

LBR (role definition files) use a text-based DSL to define characters and skills.

### File Structure

```lbr
role "Character Name" id "unique_id" {
  description "Displayed in info command";

  vars {
    "hp" = 100; "max_hp" = 100;
    "mp" = 5.0; "max_mp" = 5.0;
    "atk" = 8; "def" = 5;
    "matk" = 6; "mdef" = 4;
    "range" = 2; "speed" = 3;
  }

  tags { "saber", "knight" }

  skills {
    skill "Sword Strike" {
      range 2; targeting enemies; cooldown 1; cost mp 1;
      deal physical 10 damage to target from caster;
    }

    skill "Healing Wave" {
      range 3; targeting allies; cooldown 2; cost mp 2;
      heal 15 to target;
    }
  }
}
```

### Skill DSL Features

**Meta Information**:
- `cost mp N` - MP cost
- `range N` - Maximum range
- `cooldown N` - Turns between uses
- `targeting any|enemies|allies|self|tile` - Valid targets
- `sealed_until day D phase P` - Unlock timing
- `ends_turn` - Immediately end turn after use

**Control Flow**:
- `if <condition> then <stmt> [else <stmt>]`
- `repeat N times <stmt>`
- `parallel { stmt; stmt; }`
- `for each <selector> [in parallel] do { stmt }`
- `chance P% then <stmt> [else <stmt>]`

**Actions**:
- `deal [physical|magic] N damage to <unit> [from <unit>] [ignore defense X%]`
- `heal N to <unit>`
- `move <unit> to (x, y)`
- `dash towards <unit> up to N`
- `line [physical|magic] P to <unit> length L [radius R]`
- `add tag "tag" to <unit>` / `remove tag "tag" from <unit>`
- `set unit(<unit>) var "key" = value`

**Selectors**:
- `enemies of caster in range 4 of caster` - All enemies within 4 tiles
- `enemies of caster in range 5 of caster with tag "stunned"` - Stunned enemies
- `enemies of caster in range 100 of caster order by var "hp" asc limit 1` - Lowest HP enemy
- `nearest 3 enemies of caster` - 3 closest enemies

**⚠️ Common Syntax Errors**:

| ❌ Incorrect | ✅ Correct |
|-------------|-----------|
| `for each enemies in range 2 of caster do {` | `for each enemies of caster in range 2 of caster do {` |
| `for each enemies of caster order by var "hp" desc in range 10 of caster do {` | `for each enemies of caster in range 10 of caster order by var "hp" desc do {` |

**Clause order**: `of <unit>` → `in range` → `order by` → `limit` → `do`

📖 **Full DSL Guide**: [docs/lbr.zh-CN.md](docs/lbr.zh-CN.md) | [docs/lbr.en.md](docs/lbr.en.md)

---

## ⚙️ Configuration

### Roles Directory
Load custom roles from:
1. Command-line: `--roles <path>`
2. Environment: `LB_FATE_ROLES_DIR=<path>`
3. Default: `<app>/roles/`

### Logging

**Environment Variable**:
```bash
# Windows
set LB_FATE_LOG_LEVEL=Debug

# Linux/macOS
export LB_FATE_LOG_LEVEL=Debug
```

**Command-line Flags**:
- `--verbose` / `-v` - Verbose logging
- `--debug` / `-d` - Debug logging
- `--perf` - Enable performance tracking

**Levels**: `Verbose` > `Debug` > `Information` (default) > `Warning` > `Error` > `Fatal`

### Game Settings

| Flag | Description | Example |
|------|-------------|---------|
| `--host` | Host server mode | `--host --players 4 --port 35500` |
| `--client <addr>` | Connect to server | `--client 127.0.0.1:35500` |
| `--mode <ffa\|boss>` | Game mode | `--mode boss` |
| `--size WxH` | Grid size | `--size 15x15` |
| `--roles <path>` | Roles directory | `--roles custom_roles` |

---

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [LBR DSL Guide (Chinese)](docs/lbr.zh-CN.md) | Complete LBR syntax reference |
| [LBR DSL Guide (English)](docs/lbr.en.md) | English LBR syntax summary |
| [Avalonia Client Guide](LB_FATE.AvaloniaClient/README.md) | GUI client usage and architecture |
| [LBR Validator Guide](ETBBS.LbrValidator/README.md) | Validator tool documentation |

---

## 📄 License

This project is licensed under the MIT License - see [LICENSE.txt](LICENSE.txt) for details.

---

## 🙏 Acknowledgments

- **Avalonia UI** - Cross-platform UI framework
- **CommunityToolkit.Mvvm** - MVVM helpers
- **.NET Team** - For the amazing runtime and SDK

---

**Happy Gaming! 🎮**