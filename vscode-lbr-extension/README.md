# LBR Language Support for VS Code

<p align="center">
  <strong>为 ETBBS 的 .lbr 角色定义文件提供完整的语言支持</strong>
</p>

---

## ✨ 功能特性

| 功能 | 说明 | 快捷键 |
|------|------|--------|
| 🎨 **语法高亮** | 关键字、字符串、数字自动着色 | 自动 |
| 🧠 **智能补全** | 代码自动完成、参数提示 | `Ctrl+Space` |
| 🔍 **实时诊断** | 语法错误和语义警告 | 自动 |
| 📖 **悬停文档** | 关键字说明和用法示例 | 鼠标悬停 |
| 💡 **快速修复** | 自动修复常见错误 | `Ctrl+.` |
| 🎯 **代码格式化** | 自动缩进和对齐 | `Shift+Alt+F` |
| 🔎 **符号导航** | 快速跳转到角色和技能 | `Ctrl+T` |

---

## 🚀 快速开始

### 方式一：开发测试（推荐）

```bash
# 1. 验证环境（可选但推荐）
pwsh -File verify-setup.ps1

# 2. 安装依赖
npm install

# 3. 编译
npm run compile

# 4. 按 F5 启动扩展开发主机
```

### 方式二：打包安装

```bash
# 打包扩展
npm run package

# 安装到 VSCode
code --install-extension lbr-language-support-0.1.0.vsix
```

---

## 📖 语言特性

### 支持的 DSL 功能

#### 角色定义
```lbr
role "Knight" id "knight" {
  description "勇敢的骑士"

  vars {
    "hp" = 100
    "max_hp" = 100
    "atk" = 20
  }

  tags { "melee" "physical" }

  skills { /* ... */ }
  quotes { /* ... */ }
}
```

#### 技能系统
```lbr
skill "Power Strike" {
  range 1
  min_range 1
  targeting enemies
  mp_cost 10
  cooldown 2
  distance manhattan

  // 技能逻辑
  deal physical 25 damage to target from caster;

  chance 0.5 then {
    add tag "stunned" to target;
  }
}
```

#### 选择器
```lbr
// 基础选择器
for each enemies do { ... }
for each allies in range 3 of caster do { ... }

// 智能选择器
for each random 2 enemies do { ... }
for each healthiest 1 allies do { ... }
for each weakest 1 enemies do { ... }
for each nearest 3 units do { ... }
for each farthest 2 enemies do { ... }

// 几何选择器
for each enemies in circle radius 3 of caster do { ... }
for each enemies in cross radius 2 of caster do { ... }
for each enemies in line length 5 width 1 dir "$dir" do { ... }
for each enemies in cone radius 4 angle 60 dir "$dir" do { ... }
```

#### 控制流
```lbr
// 条件
if unit(target).has_tag("boss") then {
  deal 50 damage to target;
} else {
  deal 20 damage to target;
}

// 概率
chance 0.3 then {
  add tag "critical" to target;
}

// 循环
repeat 3 times {
  deal 5 damage to target;
}

// 并行执行
in parallel {
  { deal 10 damage to target; }
  { heal 5 to caster; }
}
```

#### 引用系统
```lbr
quotes {
  on_turn_start ["准备战斗！", "来吧！"]
  on_turn_end ["下一个！"]
  on_skill "Power Strike" ["吃我一击！"]
  on_damage ["啊！", "好痛！"]
  on_hp_below 0.5 ["我还没输！"]
  on_hp_below 0.2 ["危险..."]
  on_victory ["胜利！"]
  on_defeat ["我倒下了..."]
}
```

---

## ⚙️ 配置

在 VSCode 设置中配置：

```json
{
  // LSP 服务器路径（可选，默认使用内置服务器）
  "lbrLanguageServer.serverPath": "",

  // 界面语言：en 或 zh-CN
  "lbrLanguageServer.locale": "en",

  // LSP 追踪级别：off, messages, verbose
  "lbrLanguageServer.trace.server": "off"
}
```

---

## 🎯 使用示例

### 1. 智能补全

```lbr
skill "Test" {
  de|    ← 输入 "de" 按 Ctrl+Space

  // 会显示：
  // • deal
  // • description
}
```

### 2. 实时错误检测

```lbr
skill "Error Example" {
  range 3
  min_range 5    // ❌ 错误：min_range > range
  //        ~~~  红色波浪线

  // 悬停显示：
  // min_range (5) exceeds range (3); targets won't be valid
}
```

### 3. 快速修复

```lbr
chance 0% then {    // ⚠️ 警告：then 分支不可达
  // 代码...
}

// 按 Ctrl+. 显示：
// 💡 将概率修改为 50%
```

---

## 📚 文档

| 文档 | 说明 |
|------|------|
| [QUICKSTART.md](docs/QUICKSTART.md) | 5分钟快速开始 |
| [USAGE.md](docs/USAGE.md) | 完整使用手册 |
| [BUILD.md](docs/BUILD.md) | 构建和发布指南 |
| [DEBUG.md](DEBUG.md) | 调试和故障排除 |
| [CHANGELOG.md](docs/CHANGELOG.md) | 版本更新日志 |

---

## 🔧 故障排除

### 问题：扩展不工作

**解决方案**：

1. **运行验证脚本**
   ```bash
   pwsh -File verify-setup.ps1
   ```

2. **检查 Debug Console** (Ctrl+Shift+Y)
   ```
   应该看到：
   LBR Language Server extension is now active
   Found LSP server at: <路径>
   ```

3. **检查 Output 面板**
   - View → Output
   - 选择 "LBR Language Server"

4. **手动选择语言**
   - 打开 .lbr 文件
   - 右下角点击语言标识
   - 选择 "LBR"

详细故障排除请查看 [DEBUG.md](DEBUG.md)

---

## 📋 系统要求

- **VS Code**: 1.75.0 或更高版本
- **.NET Runtime**: 8.0 或更高版本
- **Node.js**: 18.0 或更高版本（仅开发需要）

---

## 🏗️ 项目结构

```
vscode-lbr-extension/
├── client/                 # VSCode 扩展客户端
│   ├── src/
│   │   └── extension.ts    # 扩展入口
│   └── out/                # 编译输出
├── server/                 # LSP 服务器（ETBBS.Lsp）
├── syntaxes/               # TextMate 语法文件
├── examples/               # 示例 LBR 文件
├── docs/                   # 文档
├── package.json            # 扩展清单
└── verify-setup.ps1        # 环境验证脚本
```

---

## 🤝 贡献

这是 [ETBBS](https://github.com/your-repo/ETBBS) 项目的一部分。

### 开发工作流

```bash
# 1. 克隆仓库
git clone <repo-url>
cd ETBBS/vscode-lbr-extension

# 2. 安装依赖
npm install

# 3. 编译
npm run compile

# 4. 开发模式（自动重编译）
npm run watch

# 5. 在另一个终端按 F5 启动调试
```

---

## 📄 许可证

与 ETBBS 项目相同

---

## 🎉 致谢

- 基于 ETBBS 游戏引擎
- 使用 [Language Server Protocol](https://microsoft.github.io/language-server-protocol/)
- 感谢所有贡献者

---

<p align="center">
  <strong>祝编写愉快！🚀</strong>
</p>
