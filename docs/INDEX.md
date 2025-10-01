# ETBBS 文档索引 / Documentation Index

欢迎查阅 ETBBS 项目的完整文档。Welcome to the complete ETBBS documentation.

---

## 📚 快速导航 / Quick Navigation

### 新手入门 / Getting Started

| 文档 / Document | 说明 / Description | 阅读时间 / Reading Time |
|----------------|-------------------|---------------------|
| [README (English)](../README.md) | Project overview and quick start | 10 min |
| [README (中文)](../README.zh-CN.md) | 项目概述与快速开始 | 10 分钟 |
| [Quick Reference](QUICK_REFERENCE.md) | DSL syntax cheat sheet / DSL 语法速查表 | 5 min / 5 分钟 |

### DSL 语法 / DSL Syntax

| 文档 / Document | 说明 / Description | 详细程度 / Detail Level |
|----------------|-------------------|---------------------|
| [LBR DSL (中文)](lbr.zh-CN.md) | 完整的 LBR 语法参考 | ⭐⭐⭐⭐⭐ 最详细 |
| [LBR DSL (English)](lbr.en.md) | Complete LBR syntax reference | ⭐⭐⭐⭐⭐ Most detailed |
| [Quick Reference](QUICK_REFERENCE.md) | 速查表 / Cheat sheet | ⭐⭐ Quick lookup |

### VSCode 扩展 / VSCode Extension

| 文档 / Document | 说明 / Description | 阅读时间 / Reading Time |
|----------------|-------------------|---------------------|
| [Extension README](../vscode-lbr-extension/README.md) | Extension overview / 扩展概述 | 5 min / 5 分钟 |
| [Quick Start](../vscode-lbr-extension/docs/QUICKSTART.md) | 5-minute setup / 5分钟快速设置 | 5 min / 5 分钟 |
| [Usage Guide](../vscode-lbr-extension/docs/USAGE.md) | Complete feature guide / 完整功能指南 | 15 min / 15 分钟 |
| [Build Guide](../vscode-lbr-extension/docs/BUILD.md) | Building and publishing / 构建和发布 | 10 min / 10 分钟 |
| [Troubleshooting](../vscode-lbr-extension/DEBUG.md) | Fix common issues / 故障排除 | As needed / 按需 |

### 开发工具 / Development Tools

| 文档 / Document | 说明 / Description | 类型 / Type |
|----------------|-------------------|------------|
| [LBR Validator](../ETBBS.LbrValidator/README.md) | CLI validation tool / CLI 验证工具 | Tool Guide |
| [LSP Documentation](LSP.md) | Language Server Protocol / 语言服务器 | Technical |
| [Skill Trace Guide](TRACE_USAGE_GUIDE.md) | Debugging with traces / 调试追踪系统 | Debug Tool |

### 项目架构 / Project Architecture

| 文档 / Document | 说明 / Description | 适合对象 / Audience |
|----------------|-------------------|------------------|
| [Project Overview](PROJECT_OVERVIEW.md) | Complete guide / 项目全面掌握指南 | All developers |
| [Benchmarks](Benchmarks.md) | Performance analysis / 性能分析 | Advanced |
| [Replay JSON](Replay_JSON.md) | Replay format / 回放格式 | Integration |

---

## 🎯 学习路径 / Learning Paths

### 路径 1: 游戏玩家 / Path 1: Game Player

1. **5 分钟** → [README](../README.md) 快速开始部分
2. **10 分钟** → 运行游戏，体验战斗系统
3. **按需** → 查看示例角色文件 (`publish/roles/*.lbr`)

### 路径 2: 角色设计师 / Path 2: Role Designer

1. **10 分钟** → [Quick Reference](QUICK_REFERENCE.md) 快速了解语法
2. **30 分钟** → [LBR DSL Guide](lbr.zh-CN.md) 学习完整语法
3. **5 分钟** → 安装 [VSCode Extension](../vscode-lbr-extension/README.md)
4. **实践** → 修改示例角色，创建自己的技能
5. **工具** → 使用 [LBR Validator](../ETBBS.LbrValidator/README.md) 验证语法

### 路径 3: 框架开发者 / Path 3: Framework Developer

1. **15 分钟** → [Project Overview](PROJECT_OVERVIEW.md) 理解架构
2. **30 分钟** → 阅读核心代码 (`ETBBS/`)
3. **1 小时** → 运行测试套件，理解测试覆盖
4. **30 分钟** → [LSP Documentation](LSP.md) 了解扩展机制
5. **实践** → 扩展 DSL 或添加新功能

### 路径 4: VSCode 扩展用户 / Path 4: VSCode Extension User

1. **5 分钟** → [Extension Quick Start](../vscode-lbr-extension/docs/QUICKSTART.md)
2. **10 分钟** → [Usage Guide](../vscode-lbr-extension/docs/USAGE.md) 学习所有功能
3. **按需** → [Troubleshooting](../vscode-lbr-extension/DEBUG.md) 遇到问题时查阅

---

## 🔍 按主题查找 / Find by Topic

### DSL 语法 / DSL Syntax

- **基础语法** → [lbr.zh-CN.md § 文件结构](lbr.zh-CN.md#1-文件结构)
- **选择器** → [Quick Reference § 选择器](QUICK_REFERENCE.md#-选择器速查)
- **表达式** → [Quick Reference § 表达式](QUICK_REFERENCE.md#-表达式)
- **控制流** → [Quick Reference § 控制流](QUICK_REFERENCE.md#-控制流)
- **动作系统** → [lbr.zh-CN.md](lbr.zh-CN.md) 完整列表

### 调试技能 / Debugging Skills

- **追踪系统** → [TRACE_USAGE_GUIDE.md](TRACE_USAGE_GUIDE.md)
- **验证器** → [ETBBS.LbrValidator](../ETBBS.LbrValidator/README.md)
- **VSCode 诊断** → [Extension Usage § 错误诊断](../vscode-lbr-extension/docs/USAGE.md#3-错误诊断)

### 游戏功能 / Game Features

- **Boss 模式** → [README § Boss Mode](../README.md#boss-mode)
- **AI 脚本** → [README § Boss AI Script](../README.md#game-modes)
- **网络对战** → [README § Multiplayer Mode](../README.md#multiplayer-mode)

### 开发工具 / Developer Tools

- **VSCode 扩展** → [vscode-lbr-extension/](../vscode-lbr-extension/README.md)
- **LSP 服务器** → [LSP.md](LSP.md)
- **验证器** → [LbrValidator](../ETBBS.LbrValidator/README.md)
- **追踪调试** → [TRACE_USAGE_GUIDE.md](TRACE_USAGE_GUIDE.md)

---

## 📖 文档类型说明 / Document Types

| 图标 / Icon | 类型 / Type | 说明 / Description |
|------------|-------------|-------------------|
| 📚 | 指南 / Guide | 完整的教程和指南 |
| 🚀 | 快速开始 / Quick Start | 5-10 分钟入门 |
| 📖 | 参考 / Reference | 语法和 API 参考 |
| 🛠️ | 工具 / Tool | 工具使用说明 |
| 🐛 | 调试 / Debug | 故障排除和调试 |
| 🏗️ | 架构 / Architecture | 系统设计和架构 |

---

## 🆘 获取帮助 / Getting Help

### 遇到问题？/ Having Issues?

1. **语法错误** → [LBR DSL Guide](lbr.zh-CN.md) 检查语法
2. **VSCode 扩展问题** → [Troubleshooting](../vscode-lbr-extension/DEBUG.md)
3. **性能问题** → [Benchmarks](Benchmarks.md)
4. **理解架构** → [Project Overview](PROJECT_OVERVIEW.md)

### 想要贡献？/ Want to Contribute?

1. 阅读 [Project Overview](PROJECT_OVERVIEW.md) 了解架构
2. 运行测试：`dotnet test`
3. 查看未完成的功能和改进点

---

## 📊 文档完整度 / Documentation Coverage

| 领域 / Area | 覆盖度 / Coverage | 文档 / Documents |
|------------|------------------|-----------------|
| **DSL 语法** | ⭐⭐⭐⭐⭐ 100% | lbr.zh-CN.md, lbr.en.md, QUICK_REFERENCE.md |
| **VSCode 扩展** | ⭐⭐⭐⭐⭐ 100% | vscode-lbr-extension/docs/* |
| **项目架构** | ⭐⭐⭐⭐⭐ 100% | PROJECT_OVERVIEW.md |
| **调试工具** | ⭐⭐⭐⭐⭐ 100% | TRACE_USAGE_GUIDE.md |
| **游戏玩法** | ⭐⭐⭐⭐ 80% | README.md (可扩充游戏策略) |

---

## 🔄 最后更新 / Last Updated

**日期 / Date**: 2025-10-01
**版本 / Version**: 3.5
**主要更新 / Major Updates**:
- ✅ 完整的 VSCode 扩展文档
- ✅ LSP 服务器文档
- ✅ 技能追踪系统指南
- ✅ 项目全面掌握指南

---

<p align="center">
  <strong>感谢阅读！Happy coding! 🎉</strong>
</p>
