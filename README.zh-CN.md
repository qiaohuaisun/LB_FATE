# ETBBS + LB_FATE（中文）

这是一个用于网格/回合制游戏的可复用核心库（ETBBS，.NET 8），以及一个基于该核心的控制台示例游戏（LB_FATE）。同时提供 VS Code 扩展与示例角色脚本。

语言：中文 | English see README.md

## 概览

- 核心库 `ETBBS`：实现不可变世界状态、原子行动、技能组合/执行、事件总线，以及用于定义角色与技能的文本 DSL（LBR）。
- 示例游戏 `LB_FATE`：运行 2D 网格，加载 `.lbr` 角色，支持本地与 TCP 多人。入口在 `LB_FATE/Program.Main.cs`。
- **Avalonia GUI 客户端** `LB_FATE.AvaloniaClient`：提供现代化跨平台图形界面，支持交互式游戏棋盘、实时单位状态和鼠标操控。
- VS Code 扩展位于 `vscode-extension/`：为 `.lbr` 文件提供语法高亮、代码片段、补全、格式化与基础诊断。

## 目录结构

- `ETBBS/` → 核心库
  - 状态/键/上下文：`Core/*.cs`
  - 原子行动：`Actions/*.cs`
  - 技能/执行/校验：`Skills/*.cs`
  - 角色与技能 DSL：`DSL/*.cs`
  - 系统：回合推进、回放、事件：`Systems/*.cs`
- `LB_FATE/` → 示例控制台游戏（本地或主机/客户端）
  - 入口：`Program.Main.cs`
  - 循环与 UI：`Game/*.cs`
  - 网络：`Net.cs`
  - 领域模型：`Domain.cs`
- `LB_FATE.AvaloniaClient/` → 跨平台 GUI 客户端
  - 现代化 Avalonia UI 界面
  - MVVM 架构
  - 支持鼠标操控的交互式游戏棋盘
  - 实时单位状态和战斗日志
  - 详见 `LB_FATE.AvaloniaClient/README.md`
- `roles/` → 示例 `.lbr` 角色与技能
- `vscode-extension/` → VS Code 扩展源码与打包脚本
- `publish/win-x64/` → Windows 预构建二进制与辅助脚本

## 构建

- 依赖：.NET SDK 8.0+
- 构建解决方案：
  - `dotnet build ETBBS.sln -c Release`

## 运行（示例游戏）

### Avalonia GUI 客户端（推荐）

**现代化跨平台图形界面：**

1. **启动服务器**（使用启动脚本或命令行）
2. **启动 GUI 客户端**：
   ```bash
   dotnet run --project LB_FATE.AvaloniaClient/LB_FATE.AvaloniaClient.csproj
   ```
3. **连接**：在连接选项卡输入服务器地址/端口，点击"连接服务器"
4. **游戏**：切换到游戏选项卡，使用鼠标或命令进行游戏

**功能特性**：
- 支持鼠标操控的交互式棋盘（左键移动，右键攻击）
- 实时单位状态显示（HP/MP 条）
- 技能面板，显示冷却时间
- 战斗日志
- 命令输入框，支持提示

详见 `LB_FATE.AvaloniaClient/README.md`。

### 控制台客户端（传统）

#### 快速开始（Windows）

使用 `publish/` 目录下的便捷启动脚本：
- **服务器**：`runServer.cmd` - 交互式日志级别选择
- **客户端**：`runClient.cmd` - 交互式日志级别选择
- **调试服务器**：`runServer-logs.cmd` - 详细日志 + 性能追踪
- **调试客户端**：`runClient-logs.cmd` - 详细日志 + 自动重连

详细使用指南请参阅 `publish/README_LAUNCHER.md`。

#### 命令行方式

- 本地单机（模拟 7 名玩家）：
  - `dotnet run --project LB_FATE/LB_FATE.csproj`
- 指定自定义角色目录：
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --roles roles`
- 作为主机（TCP）：
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --host --players 4 --port 35500 [--roles 路径] [--size 宽x高] [--mode ffa|boss]`
- 作为客户端：
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --client 127.0.0.1:35500`

### 日志配置

通过环境变量或命令行参数控制日志详细程度：

**环境变量**（所有平台）：
```bash
# Windows CMD
set LB_FATE_LOG_LEVEL=Debug

# Windows PowerShell
$env:LB_FATE_LOG_LEVEL="Debug"

# Linux/macOS
export LB_FATE_LOG_LEVEL=Debug
```

**命令行参数**：
- `--verbose` / `-v` - 详细日志（最大详细程度）
- `--debug` / `-d` - 调试日志（开发模式）
- `--perf` - 启用性能追踪

**日志级别**（从详细到简略）：
- `Verbose` - 所有日志包括追踪
- `Debug` - 调试信息
- `Information` - 标准日志（默认）
- `Warning` - 仅警告和错误
- `Error` - 仅错误
- `Fatal` - 仅致命错误

**使用示例**：
```bash
# 开发模式使用调试日志
dotnet run --project LB_FATE/LB_FATE.csproj -- --host --debug --perf

# 生产环境使用最小日志
LB_FATE_LOG_LEVEL=Warning dotnet run --project LB_FATE/LB_FATE.csproj -- --host

# 客户端详细日志用于故障排查
dotnet run --project LB_FATE/LB_FATE.csproj -- --client 127.0.0.1:35500 --verbose
```

### 其他环境变量

- **角色目录**：`LB_FATE_ROLES_DIR=<目录>`（递归加载）
- **游戏模式**：`LB_FATE_MODE=boss`（或 `ffa`）。命令行 `--mode` 参数优先。

### 模式说明

- `ffa`：默认自由对战模式（每位玩家互为敌方）。
- `boss`：Boss 战模式。7 名玩家分配至同一队伍，与 AI 控制的 Boss 对战。Boss 从具备“beast/冠位”相关标签/类/id 的角色中随机选取；若找不到，则回退到高生命的角色或内置 `boss_beast`。

### Boss AI 规则脚本（可选）

- 放置位置：优先 `roles/<boss_id>.ai.json`，其次 `ai/boss_default.ai.json`。
- 结构（JSON）：
  - `rules`: 规则数组，自上而下匹配；命中后执行，不再继续匹配。
    - `if`: 条件（全部满足才命中；均为可选）
      - `hp_pct_lte`: double；血量百分比阈值（0..1）。
      - `phase_in`: int[]；仅在这些阶段触发（与“按阶段扣”一致）。
      - `skill_ready`: string；技能名，就绪判定包含 MP/CD（距离由目标选择保证）。
      - `distance_lte`: int；与最近敌人的距离 ≤ N。
      - `min_hits` + `range_of`: int + string；命中估算（与 `target.radius/cluster` 配合）。
      - `has_tag`: string；自身带指定标签。
    - `target`: 目标/落点选择
      - `type`: `nearest_enemy` | `cluster` | `approach`
      - `radius`: int；cluster（Chebyshev）半径。
      - `stop_at_range_of`: string；approach 时，停在此技能/普攻的射程内。
    - `action`: `cast` | `move_to` | `basic_attack`（默认为 `cast`）。
    - `skill`: string；显式指定技能名（可省略，使用 `if.skill_ready`）。
    - `telegraph`: bool；预警技能：本阶段仅预警，不释放。
    - `telegraph_delay`: int；延迟的“阶段”数（≥1，默认 1；可跨日）。
    - `telegraph_message`: string；预警提示文案，通过 AppendPublic 公共日志输出。
  - `fallback`: 兜底动作为 `basic_attack`（可选）。

- 兜底逻辑：
  - 预警到期时若原目标失效（死亡/越界/距离不合法等），引擎自动重选：
    - tile 技能：以施法者为中心，在 `range` 内枚举格子，选择“距离最近敌人”最优的落点。
    - unit 技能：如为“直线 AoE（line aoe）”，将扫描所有敌人作为候选目标，估算“沿贪心路径、半径 R 的命中数”，选择命中最多（若并列，选更近）的目标；否则选最近敌人。
  - 预警与释放均通过 AppendPublic 输出提示。

- 示例（摘自 `roles/boss_beast.ai.json`）：

```
{
  "rules": [
    {
      "if": { "skill_ready": "Mass Sweep", "min_hits": 2, "range_of": "Mass Sweep" },
      "target": { "type": "cluster", "origin": "caster", "radius": 2 },
      "telegraph": true,
      "telegraph_delay": 1,
      "telegraph_message": "野兽蓄力横扫：下一阶段范围2"
    }
  ],
  "fallback": "basic_attack"
}
```

说明
- 旧的 `LB_FATE/Program.cs`（单文件原型）已移除以避免混淆。入口为 `LB_FATE/Program.Main.cs` 与 `LB_FATE/Game/` 下的分部 `Game` 类。

## LBR DSL（角色与技能）

- 角色文件：`role "名称" id "id" { description/vars/tags/skills }`
- 技能：命令式迷你 DSL，包含元信息与语句：
  - 元信息：`cost mp N; range N; cooldown N; targeting any|enemies|allies|self|tile; min_range N; sealed_until day D [phase P]; [ends_turn;]`（保留 `sealed_until T;` 旧语法，T 为内部回合索引，0 基）
  - 控制：`if ... then ... [else ...]`、`repeat N times ...`、`parallel { ... }`、`for each <selector> [in parallel] do { ... }`、`chance P% then ... [else ...]`
  - 动作：`deal [physical|magic] N ... [ignore ...%]`、`line ... length L [radius R]`、`heal`、`move`、`dash towards`、`add/remove tag`、
    `set unit(...) var "k" = value`、`set tile(...) var "k" = value`、`set global var "k" = value`、`consume mp = N`
  - 值引用：`var "k" of caster|target|it|unit id "..."`，支持简单 `+`/`-` 运算

更多语法示例可参考英文 `README.md` 或 `docs/` 下相关文档（若存在）。

## VS Code 扩展

- 在 VS Code 中安装 `vscode-extension/etbbs-lbr-tools-*.vsix`。
- 功能：语法、代码片段、补全、Hover、折叠、文档符号、基础诊断、格式化。

## Windows 运行

- 使用 `publish/win-x64/` 下脚本启动 host/client/local 模式。
- 角色来源依次为应用目录 `roles/`、环境变量 `LB_FATE_ROLES_DIR`、命令行 `--roles`。

## 控制台中文显示（Windows）

阶段规则：第 1/5 阶段可用全部指令；第 2~4 阶段可用 move/pass/skills/info/help/hint。

- 控制台程序已强制使用 UTF-8 输入/输出以正确显示中文。
- 若仍出现乱码，建议使用 Windows Terminal，确保字体支持中文，或在启动前执行 `chcp 65001`。

