# 构建和发布指南

## 📦 构建步骤

### 前置要求

- ✅ Node.js 18+
- ✅ .NET 8.0 SDK
- ✅ VSCode 1.75+
- ✅ PowerShell 或 Bash

---

## 🔨 完整构建流程

### 1. 准备 LSP 服务器

服务器必须先构建并复制到 `server/` 目录：

**Windows (PowerShell):**
```powershell
pwsh -File prepare-server.ps1
```

**Linux/macOS:**
```bash
chmod +x prepare-server.sh
./prepare-server.sh
```

**输出**：
```
Building ETBBS.Lsp...
Server prepared successfully!
Server location: server

Name                     Length
----                     ------
ETBBS.dll               235008
ETBBS.Lsp.exe           151552
...
```

---

### 2. 安装依赖

```bash
npm install
```

这会安装：
- `vscode-languageclient` - LSP 客户端库
- `typescript` - TypeScript 编译器
- `@vscode/vsce` - 扩展打包工具

---

### 3. 编译 TypeScript

```bash
npm run compile
```

**输出位置**: `client/out/extension.js`

**开发模式**（自动重编译）：
```bash
npm run watch
```

---

### 4. 测试扩展

在 VSCode 中按 **F5** 启动扩展开发主机

**验证**：
1. 打开 .lbr 文件
2. 检查语法高亮
3. 测试 LSP 功能

---

### 5. 打包扩展

```bash
npm run package
```

**输出**: `lbr-language-support-0.1.0.vsix`

**检查包内容**：
```bash
# Windows
Expand-Archive lbr-language-support-0.1.0.vsix -DestinationPath temp
ls temp/extension

# Linux/macOS
unzip -l lbr-language-support-0.1.0.vsix
```

**应该包含**：
- `extension/client/out/extension.js`
- `extension/server/ETBBS.Lsp.exe` (或 .dll)
- `extension/syntaxes/lbr.tmLanguage.json`
- `extension/package.json`

---

## 📥 本地安装

### 方法 1: 命令行

```bash
code --install-extension lbr-language-support-0.1.0.vsix
```

### 方法 2: VSCode UI

1. Ctrl+Shift+X 打开扩展面板
2. 点击 `...` 菜单
3. 选择 "Install from VSIX..."
4. 选择 `.vsix` 文件

---

## 🗑️ 卸载

```bash
code --uninstall-extension etbbs.lbr-language-support
```

或在扩展面板中右键点击卸载

---

## 🌐 发布到市场

### 准备发布

1. **创建发布者账号**
   - 访问 https://marketplace.visualstudio.com/manage
   - 创建发布者 ID

2. **获取 Personal Access Token (PAT)**
   - Azure DevOps → User Settings → Personal Access Tokens
   - 权限：Marketplace (Manage)

3. **登录**
   ```bash
   npx vsce login <publisher-name>
   ```

### 发布

```bash
# 方法 1: 自动发布
npx vsce publish

# 方法 2: 先打包再上传
npx vsce package
# 手动上传到 https://marketplace.visualstudio.com/manage
```

### 更新版本

```bash
# 更新 package.json 版本
npm version patch  # 0.1.0 → 0.1.1
npm version minor  # 0.1.0 → 0.2.0
npm version major  # 0.1.0 → 1.0.0

# 重新打包
npm run package
```

---

## 🧪 验证清单

发布前验证：

- [ ] `pwsh -File verify-setup.ps1` 全部通过
- [ ] `npm run compile` 无错误
- [ ] 语法高亮正常
- [ ] LSP 功能工作（补全、诊断、悬停）
- [ ] 中英文本地化正常
- [ ] 示例文件无错误
- [ ] README.md 文档完整
- [ ] CHANGELOG.md 已更新
- [ ] 版本号已更新

---

## 📂 文件检查

### 必须包含的文件

```
vscode-lbr-extension/
├── client/out/extension.js      ✓
├── server/ETBBS.Lsp.exe         ✓
├── server/ETBBS.dll             ✓
├── syntaxes/lbr.tmLanguage.json ✓
├── package.json                 ✓
├── README.md                    ✓
└── CHANGELOG.md                 ✓
```

### .vscodeignore 配置

确保 `.vscodeignore` 正确配置：

```
.vscode/**
.vscode-test/**
client/src/**
client/tsconfig.json
node_modules/**/.bin/**
**/*.map
**/*.ts
!client/out/**/*.js
```

**注意**: `!client/out/**/*.js` 确保编译输出被包含

---

## 🐛 常见问题

### 问题 1: 打包失败

```
ERROR: Missing publisher name
```

**解决**: 在 `package.json` 中设置：
```json
{
  "publisher": "your-publisher-name"
}
```

### 问题 2: 服务器未包含

**原因**: `.vscodeignore` 排除了 `server/`

**解决**: 确保 `.vscodeignore` 中没有：
```
server/**  ← 删除这行
```

### 问题 3: 打包体积过大

**检查大小**:
```bash
ls -lh lbr-language-support-0.1.0.vsix
```

**优化**:
1. 确保排除了 `node_modules`
2. 只包含 Release 版本的服务器
3. 移除不必要的文件

---

## 📊 版本管理

### 版本号规范

遵循 [Semantic Versioning](https://semver.org/):

- **Major (1.0.0)**: 破坏性更改
- **Minor (0.1.0)**: 新功能，向后兼容
- **Patch (0.0.1)**: Bug 修复

### 更新 CHANGELOG

每次发布前更新 `CHANGELOG.md`:

```markdown
## [0.1.1] - 2025-10-02

### Fixed
- 修复服务器路径检测问题
- 改进错误消息

### Added
- 添加环境验证脚本
```

---

## 🚀 自动化发布

### GitHub Actions 示例

```yaml
name: Publish Extension

on:
  release:
    types: [published]

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: 18
      - run: npm install
      - run: npm run compile
      - run: npx vsce publish -p ${{ secrets.VSCE_PAT }}
```

---

## 📖 相关文档

- [VSCode Extension API](https://code.visualstudio.com/api)
- [Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension)
- [Extension Manifest](https://code.visualstudio.com/api/references/extension-manifest)
