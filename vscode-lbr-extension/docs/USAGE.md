# LBR VSCode 扩展使用手册

## 🚀 快速启动

### 步骤 1: 准备环境

```bash
cd vscode-lbr-extension
npm install
npm run compile
```

### 步骤 2: 启动扩展

在 VSCode 中打开此文件夹，按 **F5** 键

→ 会打开新的"扩展开发主机"窗口

### 步骤 3: 打开示例文件

在新窗口中：
1. 打开 `examples/example.lbr`
2. 开始编辑！

---

## ✨ 功能使用

### 1. 语法高亮

**自动生效** - 关键字会自动着色：

```lbr
role "Knight" id "knight" {    // 蓝色
  vars { "hp" = 100 }          // 紫色数字

  skills {                     // 蓝色关键字
    skill "Attack" {
      range 1                  // 橙色
      deal physical 10 damage  // 绿色动作
    }
  }
}
```

### 2. 智能补全 (IntelliSense)

按 **Ctrl+Space** 或直接输入：

```lbr
skill "MySkill" {
  de|    ← 输入 "de" 按 Ctrl+Space

  // 会显示：
  // ✓ deal
  // ✓ description
}
```

**常用补全：**
- 输入 `for` → `for each enemies do { }`
- 输入 `if` → `if condition then { } else { }`
- 输入 `deal` → `deal physical N damage to target`

### 3. 错误诊断

**实时错误检测** - 红色波浪线显示错误：

```lbr
skill "Test" {
  range 3
  min_range 5    // ❌ 错误：min_range > range
  //        ~~~  红色波浪线
}
```

**悬停查看错误信息：**
```
min_range (5) exceeds range (3); targets won't be valid
```

### 4. 悬停文档 (Hover)

**悬停在关键字上** 查看说明：

```lbr
targeting enemies
//        ^^^^^^^
// 悬停显示：
// targeting: Skill metadata - targeting mode
//           (any|enemies|allies|self|tile/point)
```

### 5. 快速修复 (Code Actions)

**点击灯泡图标** 或按 **Ctrl+.**

```lbr
range 3
min_range 5    // ❌ 错误
// 💡 点击灯泡 → "将 min_range 设为与 range 相同"
```

### 6. 代码格式化

**Shift+Alt+F** 自动格式化：

```lbr
// 格式化前：
role "Test" id "test" {
vars { "hp" = 100; }
skills {
skill "Attack" { range 1; deal 10 damage to target; }}}

// 格式化后：
role "Test" id "test" {
  vars { "hp" = 100; }
  skills {
    skill "Attack" {
      range 1;
      deal 10 damage to target;
    }
  }
}
```

### 7. 符号搜索 (Symbol Navigation)

**Ctrl+T** 搜索角色/技能：

```
Ctrl+T → 输入 "Attack"
// 结果：
// 📄 example.lbr: Attack (skill)
```

**Ctrl+Shift+O** 查看当前文件大纲

---

## 🎨 语言特性

### 选择器示例

```lbr
// 基础选择器
for each enemies do { ... }
for each allies do { ... }

// 距离选择器
for each nearest 3 enemies do { ... }
for each farthest 2 allies do { ... }

// 智能选择器
for each random 2 enemies do { ... }
for each healthiest 1 allies do { ... }
for each weakest 1 enemies do { ... }

// 范围选择器
for each enemies in range 3 of caster do { ... }
for each enemies in range 2 of point do { ... }

// 几何选择器
for each enemies in circle radius 3 of caster do { ... }
for each enemies in line length 5 width 1 dir "$dir" do { ... }
for each enemies in cone radius 4 angle 60 dir "$dir" do { ... }
```

### 动作示例

```lbr
// 伤害
deal physical 10 damage to target from caster;
deal magic 15 damage to target from caster;
deal 20 damage to target;  // true damage

// 治疗
heal 10 to target;

// 变量操作
set unit(target) var "hp" = 50;
set global var "turn_count" = 1;
set tile (0,0) var "marked" = 1;

// 标签操作
add tag "stunned" to target;
remove tag "buffed" from target;
add global tag "battle_started";

// 移动
move target to (1,1);
dash target towards caster 2;
```

### 控制流示例

```lbr
// 条件
if unit(target).has_tag("boss") then {
  deal 50 damage to target;
} else {
  deal 20 damage to target;
}

// 概率
chance 0.5 then {
  add tag "critical" to target;
}

// 循环
repeat 3 times {
  deal 5 damage to target;
}

// 并行
in parallel {
  { deal 10 damage to target; }
  { heal 5 to caster; }
}
```

---

## ⚙️ 配置

### 1. 设置语言

**File → Preferences → Settings**

搜索 `lbrLanguageServer.locale`

```json
{
  "lbrLanguageServer.locale": "zh-CN"  // 或 "en"
}
```

### 2. 调试 LSP 服务器

启用详细日志：

```json
{
  "lbrLanguageServer.trace.server": "verbose"
}
```

查看日志：**View → Output → LBR Language Server**

### 3. 自定义服务器路径

如果要使用自己编译的服务器：

```json
{
  "lbrLanguageServer.serverPath": "D:/path/to/ETBBS.Lsp.exe"
}
```

---

## 🔧 故障排查

### 扩展未激活

**症状**: 打开 .lbr 文件无语法高亮

**解决**:
1. 检查文件扩展名是 `.lbr`
2. 重新加载窗口：Ctrl+R
3. 查看扩展是否启用：Extensions 面板

### LSP 服务器未启动

**症状**: 无自动补全、无错误诊断

**解决**:
1. 查看 Output 面板 → LBR Language Server
2. 检查 `server/` 目录是否存在
3. 重新运行 `pwsh -File prepare-server.ps1`

### 编译错误

**症状**: `npm run compile` 失败

**解决**:
```bash
rm -rf node_modules
npm install
npm run compile
```

### 无法打包

**症状**: `npm run package` 报错

**解决**:
```bash
npm install -g @vscode/vsce
npm run package
```

---

## 📦 打包和安装

### 开发模式（推荐用于测试）

```bash
# 按 F5 启动扩展开发主机
code --extensionDevelopmentPath=.
```

### 打包为 VSIX

```bash
npm run package
# 生成: lbr-language-support-0.1.0.vsix
```

### 安装到本地 VSCode

```bash
code --install-extension lbr-language-support-0.1.0.vsix
```

### 卸载

```bash
code --uninstall-extension etbbs.lbr-language-support
```

---

## 🎯 实用技巧

### 1. 快速创建技能模板

输入 `skill` 然后按 Tab：

```lbr
skill "NewSkill" {
  range 1
  targeting enemies

  deal physical 10 damage to target from caster;
}
```

### 2. 使用代码片段

创建 `.vscode/lbr.code-snippets`：

```json
{
  "For Each Enemies": {
    "prefix": "foreach-enemies",
    "body": [
      "for each enemies in range ${1:3} of caster do {",
      "\t${2:deal physical 10 damage to it from caster;}",
      "}"
    ]
  }
}
```

### 3. 多文件项目

```
my-game/
├── roles/
│   ├── knight.lbr
│   ├── mage.lbr
│   └── archer.lbr
└── .vscode/
    └── settings.json
```

打开整个文件夹即可享受所有 LSP 功能！

---

## 📚 更多资源

- **示例文件**: `examples/example.lbr`
- **语法参考**: 项目根目录的 `docs/lbr.zh-CN.md`
- **DSL 文档**: `DSL_IMPROVEMENTS_V2.md`
- **构建指南**: `BUILD.md`
- **快速开始**: `QUICKSTART.md`

---

## 🆘 获取帮助

1. 查看 Output 面板日志
2. 启用 verbose 追踪
3. 查看 Debug Console (Ctrl+Shift+Y)
4. 提交 Issue 到项目仓库

祝编写愉快！🎉
