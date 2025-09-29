# ETBBS + LB_FATE（中文）

这是一个用于网格/回合制游戏的可复用核心库（ETBBS，.NET 8），以及一个基于该核心的控制台示例游戏（LB_FATE）。同时提供 VS Code 扩展与示例角色脚本。

语言：中文 | English see README.md

## 概览

- 核心库 `ETBBS`：实现不可变世界状态、原子行动、技能组合/执行、事件总线，以及用于定义角色与技能的文本 DSL（LBR）。
- 示例游戏 `LB_FATE`：运行 2D 网格，加载 `.lbr` 角色，支持本地与 TCP 多人。入口在 `LB_FATE/Program.Main.cs`。
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
- `roles/` → 示例 `.lbr` 角色与技能
- `vscode-extension/` → VS Code 扩展源码与打包脚本
- `publish/win-x64/` → Windows 预构建二进制与辅助脚本

## 构建

- 依赖：.NET SDK 8.0+
- 构建解决方案：
  - `dotnet build ETBBS.sln -c Release`

## 运行（示例游戏）

- 本地单机（模拟 7 名玩家）：
  - `dotnet run --project LB_FATE/LB_FATE.csproj`
- 指定自定义角色目录：
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --roles roles`
- 作为主机（TCP）：
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --host --players 4 --port 35500 [--roles 路径] [--size 宽x高]`
- 作为客户端：
  - `dotnet run --project LB_FATE/LB_FATE.csproj -- --client 127.0.0.1:35500`
- 通过环境变量覆盖角色目录（递归加载）：
  - `LB_FATE_ROLES_DIR=<目录>`

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

