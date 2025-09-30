# ETBBS + LB_FATE

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.txt)

**语言**: [English](README.md) | 中文

一个由Claude code 协助完成，可复用的 .NET 8 网格回合制策略游戏框架，提供不可变状态引擎、文本技能 DSL，以及可运行的示例控制台游戏。

---

## 📋 目录

- [特性](#-特性)
- [快速开始](#-快速开始)
- [架构](#-架构)
- [目录结构](#-目录结构)
- [开始使用](#-开始使用)
  - [系统要求](#系统要求)
  - [构建](#构建)
  - [运行游戏](#运行游戏)
- [客户端](#-客户端)
  - [控制台客户端](#控制台客户端)
- [开发工具](#-开发工具)
  - [LBR 验证器](#lbr-验证器)
- [游戏模式](#-游戏模式)
- [LBR DSL](#-lbr-dsl角色与技能)
- [配置](#-配置)
- [文档](#-文档)
- [许可证](#-许可证)

---

## ✨ 特性

### 核心框架 (ETBBS)
- **不可变世界状态**：可预测、可测试的游戏逻辑与原子行动
- **文本技能 DSL**：使用 `.lbr` 文件定义角色与技能
- **事件总线系统**：响应式游戏事件与状态变更
- **回合制执行**：基于阶段的回合系统与验证
- **灵活目标选择**：支持单体、范围、直线攻击和选择器

### 示例游戏 (LB_FATE)
- **2D 网格战斗**：可自定义网格的战术回合制战斗
- **TCP 多人游戏**：主机/客户端网络架构
- **Boss 模式**：7 人合作对抗使用脚本决策树的 AI Boss
- **混战模式**：传统大逃杀玩法
- **控制台界面**：为终端游戏提供的文本界面

### 开发者体验
- **语法验证器**：在运行前验证 `.lbr` 文件的 CLI 工具
- **VS Code 扩展**：语法高亮、补全、代码片段和诊断
- **全面日志**：多级别日志与性能追踪
- **热重载角色**：从目录加载自定义角色

---

## 🚀 快速开始

```bash
# 本地单人（模拟 7 个 AI 玩家）
dotnet run --project LB_FATE

# 主机多人服务器
dotnet run --project LB_FATE -- --host --players 4 --port 35500

# 作为客户端连接
dotnet run --project LB_FATE -- --client 127.0.0.1:35500
```

---

## 🏗 架构

```
┌──────────────────────────────────────────────────────┐
│                   LB_FATE (游戏)                     │
│  ┌────────────┐  ┌────────────┐                     │
│  │  控制台UI  │  │  TCP 网络  │                     │
│  │            │  │ 主机/客户端│                     │
│  └──────┬─────┘  └──────┬─────┘                     │
│         └────────────────┘                           │
└───────────────────┬──────────────────────────────────┘
                    │
┌───────────────────▼──────────────────────────────────┐
│              ETBBS 核心库                            │
│  ┌─────────┐  ┌──────────┐  ┌──────────┐           │
│  │  世界   │  │  行动    │  │  技能    │           │
│  │  状态   │◄─┤ （原子） │◄─┤   DSL    │           │
│  └────┬────┘  └──────────┘  └──────────┘           │
│       │                                              │
│  ┌────▼────┐  ┌──────────┐  ┌──────────┐           │
│  │  回合   │  │  事件    │  │LBR 解析器│           │
│  │  系统   │  │  总线    │  │          │           │
│  └─────────┘  └──────────┘  └──────────┘           │
└──────────────────────────────────────────────────────┘
```

**核心概念**：
- **不可变状态**：每个行动产生新的世界状态
- **原子行动**：可组合、已验证的行动（伤害、治疗、移动等）
- **技能脚本**：文本 DSL 编译为行动序列
- **事件驱动**：状态变化发出 UI/日志事件

---

## 📁 目录结构

```
ETBBS/
├── ETBBS/                      # 核心库
│   ├── Core/                   # 世界状态、键、上下文
│   ├── Actions/                # 原子行动（伤害、治疗、移动）
│   ├── Skills/                 # 技能执行与验证
│   ├── DSL/                    # LBR 解析器与编译器
│   └── Systems/                # 回合系统、事件、回放
│
├── LB_FATE/                    # 示例控制台游戏
│   ├── Game/                   # 游戏循环、初始化、回合逻辑
│   ├── Program.Main.cs         # 入口点
│   └── Net.cs                  # TCP 网络
│
├── ETBBS.LbrValidator/         # CLI 验证工具
│   ├── Program.cs              # 验证器逻辑
│   └── README.md               # 验证器文档
│
├── ETBBS.Tests/                # 单元测试
├── LB_FATE.Tests/              # 集成测试
│
├── docs/                       # 文档
│   ├── lbr.zh-CN.md            # LBR DSL 指南（中文）
│   └── lbr.en.md               # LBR DSL 指南（英文）
│
├── publish/                    # 发布版
│   ├── roles/                  # 示例角色文件 (.lbr)
│   ├── runServer.cmd           # 服务器启动器
│   ├── runClient.cmd           # 客户端启动器
│   └── README_LAUNCHER.md      # 启动器指南
│
└── vscode-extension/           # VS Code 扩展
    └── etbbs-lbr-tools-*.vsix  # 可安装扩展
```

---

## 🎯 开始使用

### 系统要求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本
- （可选）[VS Code](https://code.visualstudio.com/) 用于编辑 `.lbr` 文件

### 构建

```bash
# 克隆仓库
git clone https://github.com/qiaohuaisun/LB_FATE.git
cd ETBBS

# 还原并构建
dotnet restore
dotnet build ETBBS.sln -c Release

# 运行测试
dotnet test
```

### 运行游戏

#### 本地模式（单人）
```bash
dotnet run --project LB_FATE
```
模拟 7 个 AI 控制的玩家进行混战。

#### 多人模式

**主机服务器**：
```bash
dotnet run --project LB_FATE -- --host --players 4 --port 35500 --mode ffa
```

**连接客户端**：
```bash
dotnet run --project LB_FATE -- --client 127.0.0.1:35500
```

#### Boss 模式
```bash
dotnet run --project LB_FATE -- --host --mode boss --players 7
```
7 名玩家合作对抗具有脚本行为的 AI Boss。

---

## 🖥 控制台客户端

**文本界面适用于：**
- 终端爱好者
- 低资源环境
- 脚本和自动化

**启动脚本**（Windows）：

| 脚本 | 用途 | 日志 |
|------|------|------|
| `publish/runServer.cmd` | 标准服务器 | 交互式选择 |
| `publish/runServer-logs.cmd` | 调试服务器 | 详细 + 性能追踪 |
| `publish/runClient.cmd` | 标准客户端 | 交互式选择 |
| `publish/runClient-logs.cmd` | 调试客户端 | 详细 + 自动重连 |

**命令**（阶段 2-4）：
- `move x y` - 移动到 (x, y)。消耗：0.5 MP
- `attack P#` - 攻击玩家。消耗：0.5 MP
- `cast <技能> [目标]` - 使用技能
- `pass` - 结束回合
- `info` - 显示单位详情
- `help` - 列出可用命令

---

## 🛠 开发工具

### LBR 验证器

在运行前验证 `.lbr` 文件以尽早捕获语法错误。

**用法**：
```bash
# 验证目录中的所有文件
dotnet run --project ETBBS.LbrValidator -- publish/roles -v

# 递归扫描并显示详情
dotnet run --project ETBBS.LbrValidator -- publish/roles -r -d

# 安静模式（仅摘要）
dotnet run --project ETBBS.LbrValidator -- publish/roles -q
```

**功能**：
- ✅ 批量验证多个文件
- ✅ 递归目录扫描
- ✅ 彩色终端输出（✓/✗）
- ✅ 详细错误消息与行号
- ✅ CI/CD 友好（退出码：0 = 成功，1 = 错误）

**示例输出**：
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

📖 **完整指南**：[ETBBS.LbrValidator/README.md](ETBBS.LbrValidator/README.md)

---

## 🎮 游戏模式

### 混战模式 (FFA)
- **默认模式**：7 名玩家争夺生存
- **胜利条件**：最后存活的玩家
- **网格**：默认 10x10（可用 `--size WxH` 自定义）

### Boss 模式
- **合作**：7 名玩家对抗 1 个 AI 控制的 Boss
- **Boss 选择**：带有 `beast`/`grand` 标签的角色（如 `beast_florence`）
- **AI 行为**：在 `roles/<boss_id>.ai.json` 中定义
- **胜利条件**：在回合限制内击败 Boss

**Boss AI 脚本示例**：
```json
{
  "rules": [
    {
      "if": {
        "hp_pct_lte": 0.5,
        "skill_ready": "狂暴模式",
        "phase_in": [3, 4]
      },
      "target": { "type": "nearest_enemy" },
      "action": "cast",
      "skill": "狂暴模式"
    },
    {
      "if": { "distance_lte": 2, "skill_ready": "群体横扫" },
      "target": { "type": "cluster", "radius": 2 },
      "telegraph": true,
      "telegraph_delay": 1,
      "telegraph_message": "Boss 正在蓄力群体横扫！"
    }
  ],
  "fallback": "basic_attack"
}
```

**预警系统**：Boss 提前一个阶段宣布强力攻击，给玩家时间应对。

---

## 📝 LBR DSL（角色与技能）

LBR（角色定义文件）使用基于文本的 DSL 定义角色和技能。

### 文件结构

```lbr
role "角色名称" id "unique_id" {
  description "在 info 命令中显示";

  vars {
    "hp" = 100; "max_hp" = 100;
    "mp" = 5.0; "max_mp" = 5.0;
    "atk" = 8; "def" = 5;
    "matk" = 6; "mdef" = 4;
    "range" = 2; "speed" = 3;
  }

  tags { "saber", "knight" }

  skills {
    skill "剑击" {
      range 2; targeting enemies; cooldown 1; cost mp 1;
      deal physical 10 damage to target from caster;
    }

    skill "治疗波" {
      range 3; targeting allies; cooldown 2; cost mp 2;
      heal 15 to target;
    }
  }
}
```

### 技能 DSL 功能

**元信息**：
- `cost mp N` - MP 消耗
- `range N` - 最大射程
- `cooldown N` - 使用间隔回合数
- `targeting any|enemies|allies|self|tile` - 有效目标
- `sealed_until day D phase P` - 解锁时机
- `ends_turn` - 使用后立即结束回合

**控制流**：
- `if <条件> then <语句> [else <语句>]`
- `repeat N times <语句>`
- `parallel { 语句; 语句; }`
- `for each <选择器> [in parallel] do { 语句 }`
- `chance P% then <语句> [else <语句>]`

**行动**：
- `deal [physical|magic] N damage to <单位> [from <单位>] [ignore defense X%]`
- `heal N to <单位>`
- `move <单位> to (x, y)`
- `dash towards <单位> up to N`
- `line [physical|magic] P to <单位> length L [radius R]`
- `add tag "标签" to <单位>` / `remove tag "标签" from <单位>`
- `set unit(<单位>) var "键" = 值`

**选择器**：
- `enemies of caster in range 4 of caster` - 4 格内所有敌人
- `enemies of caster in range 5 of caster with tag "stunned"` - 晕眩敌人
- `enemies of caster in range 100 of caster order by var "hp" asc limit 1` - HP 最低的敌人
- `nearest 3 enemies of caster` - 最近的 3 个敌人

**⚠️ 常见语法错误**：

| ❌ 错误 | ✅ 正确 |
|--------|--------|
| `for each enemies in range 2 of caster do {` | `for each enemies of caster in range 2 of caster do {` |
| `for each enemies of caster order by var "hp" desc in range 10 of caster do {` | `for each enemies of caster in range 10 of caster order by var "hp" desc do {` |

**子句顺序**：`of <单位>` → `in range` → `order by` → `limit` → `do`

📖 **完整 DSL 指南**：[docs/lbr.zh-CN.md](docs/lbr.zh-CN.md) | [docs/lbr.en.md](docs/lbr.en.md)

---

## ⚙️ 配置

### 角色目录
从以下位置加载自定义角色：
1. 命令行：`--roles <路径>`
2. 环境变量：`LB_FATE_ROLES_DIR=<路径>`
3. 默认：`<应用>/roles/`

### 日志

**环境变量**：
```bash
# Windows
set LB_FATE_LOG_LEVEL=Debug

# Linux/macOS
export LB_FATE_LOG_LEVEL=Debug
```

**命令行标志**：
- `--verbose` / `-v` - 详细日志
- `--debug` / `-d` - 调试日志
- `--perf` - 启用性能追踪

**级别**：`Verbose` > `Debug` > `Information`（默认）> `Warning` > `Error` > `Fatal`

### 游戏设置

| 标志 | 说明 | 示例 |
|------|------|------|
| `--host` | 主机服务器模式 | `--host --players 4 --port 35500` |
| `--client <地址>` | 连接到服务器 | `--client 127.0.0.1:35500` |
| `--mode <ffa\|boss>` | 游戏模式 | `--mode boss` |
| `--size WxH` | 网格大小 | `--size 15x15` |
| `--roles <路径>` | 角色目录 | `--roles custom_roles` |

---

## 📚 文档

| 文档 | 说明 |
|------|------|
| [LBR DSL 指南（中文）](docs/lbr.zh-CN.md) | 完整 LBR 语法参考 |
| [LBR DSL 指南（英文）](docs/lbr.en.md) | 英文 LBR 语法摘要 |
| [LBR 验证器指南](ETBBS.LbrValidator/README.md) | 验证器工具文档 |

---

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE.txt](LICENSE.txt)。

---

## 🙏 致谢

- **.NET 团队** - 卓越的运行时和 SDK
- **Claude code** - 强大的 Agentic Coding 工具

---

**游戏愉快！ 🎮**