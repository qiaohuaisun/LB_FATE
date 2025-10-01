# ETBBS + LB_FATE

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.txt)

**Language**: English | [中文](README.zh-CN.md)

A reusable .NET 8 framework for grid-based turn-based strategy games, featuring an immutable state engine, text-based skill DSL, and a playable sample console game. Assisted by Claude Code

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
  - [Console Client](#console-client)
- [Development Tools](#-development-tools)
  - [LBR Validator](#lbr-validator)
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
- **Console Interface**: Text-based interface for terminal play

### Developer Experience
- **Syntax Validator**: CLI tool to validate `.lbr` files before runtime
- **VSCode Extension with LSP**: Full language server integration with syntax highlighting, IntelliSense, diagnostics, hover docs, code actions, and formatting
- **Comprehensive Logging**: Multiple log levels with performance tracking
- **Hot-reloadable Roles**: Load custom roles from directories
- **Skill Trace Debugger**: Step-by-step execution tracing for debugging complex skills

---

## 🚀 Quick Start

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
│  ┌────────────┐  ┌────────────┐                     │
│  │ Console UI │  │  TCP Net   │                     │
│  │            │  │ Host/Client│                     │
│  └──────┬─────┘  └──────┬─────┘                     │
│         └────────────────┘                           │
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
├── ETBBS.LbrValidator/         # CLI validation tool
│   ├── Program.cs              # Validator logic
│   └── README.md               # Validator documentation
│
├── ETBBS.Tests/                # Unit tests
├── LB_FATE.Tests/              # Integration tests
│
├── ETBBS.Lsp/                  # LSP server for VSCode
│   └── Program.cs              # Language server implementation
│
├── docs/                       # Documentation
│   ├── lbr.zh-CN.md            # LBR DSL guide (Chinese)
│   ├── lbr.en.md               # LBR DSL guide (English)
│   ├── LSP.md                  # LSP server documentation
│   ├── PROJECT_OVERVIEW.md     # Comprehensive project guide
│   ├── TRACE_USAGE_GUIDE.md    # Skill trace debugger guide
│   ├── QUICK_REFERENCE.md      # DSL quick reference card
│   └── ...                     # Additional documentation
│
├── publish/                    # Distribution
│   ├── roles/                  # Example role files (.lbr)
│   ├── runServer.cmd           # Server launcher
│   ├── runClient.cmd           # Client launcher
│   └── README_LAUNCHER.md      # Launcher guide
│
└── vscode-lbr-extension/       # VSCode extension (full LSP integration)
    ├── client/                 # TypeScript LSP client
    ├── server/                 # Compiled LSP server binaries
    ├── syntaxes/               # TextMate grammar for syntax highlighting
    ├── docs/                   # Extension documentation
    │   ├── INDEX.md            # Documentation index
    │   ├── QUICKSTART.md       # 5-minute setup guide
    │   ├── USAGE.md            # Complete user manual
    │   └── BUILD.md            # Build and publish guide
    ├── prepare-server.ps1      # Build and copy LSP server
    ├── verify-setup.ps1        # Environment verification
    ├── DEBUG.md                # Troubleshooting guide
    └── package.json            # Extension manifest
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

## 🖥 Console Client

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

### VSCode Extension

Get full language support for `.lbr` files with the official VSCode extension.

**Features**:
- ✅ Syntax highlighting with TextMate grammar
- ✅ IntelliSense auto-completion (context-aware)
- ✅ Real-time diagnostics and error checking
- ✅ Hover documentation for keywords
- ✅ Quick fixes and code actions
- ✅ Code formatting (auto-indent)
- ✅ Workspace symbol search
- ✅ Multi-language support (English/中文)

**Installation**:

1. **Option 1: Install from VSIX** (Recommended)
   ```bash
   cd vscode-lbr-extension
   pwsh -File verify-setup.ps1  # Verify environment
   npm install && npm run compile
   npm run package              # Creates .vsix file
   code --install-extension lbr-language-support-*.vsix
   ```

2. **Option 2: Development Mode**
   ```bash
   cd vscode-lbr-extension
   pwsh -File prepare-server.ps1  # Build LSP server
   npm install && npm run compile
   # Press F5 in VSCode to launch Extension Development Host
   ```

**Quick Start**:
- Open any `.lbr` file in VSCode
- Enjoy syntax highlighting and IntelliSense
- Hover over keywords for documentation
- Press `Ctrl+.` for quick fixes

📖 **Full Guide**: [vscode-lbr-extension/README.md](vscode-lbr-extension/README.md)
📖 **Documentation**: [vscode-lbr-extension/docs/INDEX.md](vscode-lbr-extension/docs/INDEX.md)
🐛 **Troubleshooting**: [vscode-lbr-extension/DEBUG.md](vscode-lbr-extension/DEBUG.md)

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

**💡 Flexible Syntax** (v2.0+):

Selector clauses can now appear in **any order**:

```lbr
# All valid!
for each enemies of caster in range 4 of caster limit 2 do { ... }
for each enemies limit 2 in range 4 of caster of caster do { ... }
for each enemies in range 4 of caster order by var "hp" asc limit 1 do { ... }
```

**Enhanced Error Messages**:
- Duplicate clause detection
- Helpful suggestions for common mistakes
- Clear syntax error locations with caret indicators

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

### Core Documentation

| Document | Description |
|----------|-------------|
| [LBR DSL Guide (Chinese)](docs/lbr.zh-CN.md) | Complete LBR syntax reference with examples |
| [LBR DSL Guide (English)](docs/lbr.en.md) | English LBR syntax summary |
| [Project Overview](docs/PROJECT_OVERVIEW.md) | Comprehensive guide to architecture and features |
| [Quick Reference](docs/QUICK_REFERENCE.md) | Handy DSL syntax cheat sheet |
| [Skill Trace Guide](docs/TRACE_USAGE_GUIDE.md) | Debugging skills with execution tracing |
| [LSP Documentation](docs/LSP.md) | Language Server Protocol implementation details |

### Tools Documentation

| Document | Description |
|----------|-------------|
| [LBR Validator](ETBBS.LbrValidator/README.md) | CLI validator tool for `.lbr` files |
| [VSCode Extension](vscode-lbr-extension/README.md) | Full-featured VSCode extension guide |
| [Extension Quick Start](vscode-lbr-extension/docs/QUICKSTART.md) | 5-minute setup guide |
| [Extension Usage](vscode-lbr-extension/docs/USAGE.md) | Complete feature walkthrough |
| [Extension Build Guide](vscode-lbr-extension/docs/BUILD.md) | Building and publishing the extension |
| [Extension Troubleshooting](vscode-lbr-extension/DEBUG.md) | Debugging extension issues |

### Additional Resources

| Document | Description |
|----------|-------------|
| [Replay JSON Format](docs/Replay_JSON.md) | Replay file structure and usage |
| [Benchmarks](docs/Benchmarks.md) | Performance benchmarks and analysis |

---

## 📄 License

This project is licensed under the MIT License - see [LICENSE.txt](LICENSE.txt) for details.

---

## 👨‍💻 About the Author

Hi! I'm a freshman student who created this game library and sample game using **Claude Code**. This is my first time developing a project of this scale, and I've learned a lot throughout the development process.

### 🎮 Testing & Feedback Welcome

I warmly welcome everyone to:
- **Test the game**: Find bugs or suggest improvements
- **Share feedback**: Tell me about your gaming experience and ideas
- **Submit characters**: Design and contribute your own `.lbr` character files! I'll carefully review and include excellent submissions

### 🤝 Looking for Collaborators

If you're interested in this project and willing to help me develop and improve this game framework, I would be extremely grateful! Although I'm a student, I can offer **modest compensation** as a token of appreciation.

Whether you're interested in:
- 🎨 **Art & Design**: UI, character portraits, icons, etc.
- 💻 **Programming**: New features, performance optimization, bug fixes
- 📝 **Content Creation**: Character design, storylines, game balance
- 🌍 **Localization**: Multi-language translation and documentation

All contributions are welcome!

### 📧 Contact

For any questions, suggestions, or collaboration inquiries, please feel free to contact me:

**Email**: qiaohuaisun@gmail.com

Looking forward to hearing from you!

---

## 🙏 Acknowledgments

Special thanks to:
- **Dawen** and **Xiaomao** - My dear friends who provided tremendous support and advice
- **Claude Code** - The powerful AI coding assistant that made this project possible
- **.NET Team** - For the amazing runtime and SDK

Thanks to all testers, contributors, and supporters!

---

**Happy Gaming! 🎮**