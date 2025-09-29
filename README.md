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
- `roles/` — Example `.lbr` roles and skills
- `docs/` — DSL docs (Chinese), plus this README references
- `vscode-extension/` — VS Code extension sources and packaged `.vsix`
- `publish/win-x64/` — Prebuilt Windows binaries and helper scripts

## Build

- Requirements: .NET SDK 8.0+
- Build solution:
  - `dotnet build ETBBS.sln -c Release`

## Run (Sample Game)

- Local single‑console (7 players simulated):
  - `dotnet run --project LB_FATE/LB_FATE.csproj`
- Use custom roles folder:
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --roles roles`
- Host server (TCP):
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --host --players 4 --port 35500 [--roles PATH] [--size WxH]`
- Client:
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --client 127.0.0.1:35500`
- Environment override for roles:
  - `LB_FATE_ROLES_DIR=<dir>` (recursively loaded)

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

## VS Code Extension

- Install `vscode-extension/etbbs-lbr-tools-*.vsix` in VS Code.
- Features: syntax, snippets, completions, hovers, folding, symbols, basic diagnostics, formatter.

## Windows Build

- Use scripts under `publish/win-x64/` to start host/client/local modes.
- Roles are loaded from the app `roles/` folder, `LB_FATE_ROLES_DIR`, or `--roles`.
  - Verbose variants: `runServer-logs.cmd` (enables `LB_FATE_SERVER_LOGS=1`, `LB_FATE_DEBUG_LOGS=1`), `runClient-logs.cmd` (auto-reconnect hints).

## Encoding Note (Windows)

- The console app now forces UTF-8 I/O so Chinese text renders correctly.
- If you still see garbled characters, use Windows Terminal, ensure the font supports CJK, or run `chcp 65001` before starting.


