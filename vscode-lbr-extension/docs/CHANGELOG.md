# 更新日志

所有重要的项目变更都会记录在此文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

---

## [0.1.0] - 2025-10-01

### 🎉 初始版本

#### 新增功能

**核心功能**
- ✅ 完整的 LSP 集成（基于 ETBBS.Lsp 服务器）
- ✅ .lbr 文件语法高亮（TextMate 语法）
- ✅ 智能代码补全（IntelliSense）
- ✅ 实时错误诊断和警告
- ✅ 悬停文档显示
- ✅ 代码快速修复（Code Actions）
- ✅ 自动代码格式化
- ✅ 工作区符号搜索

**语言支持**
- ✅ 角色定义（role, vars, tags, skills）
- ✅ 技能元数据（range, mp_cost, cooldown, targeting, distance）
- ✅ 控制流语句（for, if, chance, repeat, parallel）
- ✅ 动作系统（deal, heal, set, add, remove, move, dash）
- ✅ 选择器（enemies, allies, nearest, farthest, random, healthiest, weakest）
- ✅ 几何形状（circle, cross, line, cone）
- ✅ 距离度量（manhattan, chebyshev, euclidean）
- ✅ 引用系统（quotes）

**本地化**
- ✅ 英文界面 (en)
- ✅ 中文界面 (zh-CN)
- ✅ 可配置的语言切换

**工具**
- ✅ 环境验证脚本 (`verify-setup.ps1`)
- ✅ 服务器准备脚本 (`prepare-server.ps1/sh`)
- ✅ 示例 LBR 文件

#### 技术特性

- LSP 客户端基于 `vscode-languageclient` v9.0.1
- TypeScript 5.3.0
- 支持 VSCode 1.75.0+
- .NET 8.0 运行时
- 完整的错误处理和日志记录

#### 文档

- ✅ README.md - 项目概述
- ✅ QUICKSTART.md - 快速开始指南
- ✅ USAGE.md - 完整使用手册
- ✅ BUILD.md - 构建和发布指南
- ✅ DEBUG.md - 故障排除指南

### 🔧 已知问题

- Windows 上需要 PowerShell 运行准备脚本
- Linux/macOS 需要确保 `.sh` 脚本有执行权限

### 🚀 未来计划

#### v0.2.0
- [ ] 代码片段（Snippets）支持
- [ ] 跳转到定义（Go to Definition）
- [ ] 查找所有引用（Find All References）
- [ ] 符号重命名（Rename Symbol）

#### v0.3.0
- [ ] 代码折叠（Folding Ranges）
- [ ] 语义高亮（Semantic Highlighting）
- [ ] 多文件项目支持
- [ ] 工作区配置文件

#### v1.0.0
- [ ] 调试器集成
- [ ] 测试框架集成
- [ ] 性能优化
- [ ] 完整的文档网站

---

## [Unreleased]

### 计划中
- 增强的代码补全（上下文感知）
- 更多的快速修复建议
- 代码重构工具
- 集成 AI 辅助编码

---

## 版本说明

### 版本号格式: MAJOR.MINOR.PATCH

- **MAJOR**: 不兼容的 API 更改
- **MINOR**: 向后兼容的新功能
- **PATCH**: 向后兼容的问题修复

### 更新类型

- **Added** (新增): 新功能
- **Changed** (变更): 现有功能的变化
- **Deprecated** (弃用): 即将移除的功能
- **Removed** (移除): 已移除的功能
- **Fixed** (修复): Bug 修复
- **Security** (安全): 安全性更新

---

## 贡献

如果你发现问题或有改进建议，请：

1. 提交 Issue 到项目仓库
2. 查看 [CONTRIBUTING.md](../CONTRIBUTING.md)
3. 提交 Pull Request

---

<p align="center">
  <strong>感谢使用 LBR Language Support！</strong>
</p>
