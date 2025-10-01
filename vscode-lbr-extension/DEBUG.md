# 调试 VSCode 扩展

## 问题诊断步骤

### 1. 检查扩展是否激活

**按 F5 启动后**，在扩展开发主机窗口：

1. 打开 **Debug Console** (Ctrl+Shift+Y)
2. 查看是否有日志：
   ```
   LBR Language Server extension is now active
   Found LSP server at: <路径>
   ```

**如果没有看到这些日志**：
- 扩展未激活
- 检查是否打开了 .lbr 文件

### 2. 检查 LSP 服务器状态

在扩展开发主机窗口：

1. **View → Output** (或 Ctrl+Shift+U)
2. 下拉框选择 **"LBR Language Server"**
3. 查看日志

**正常日志示例**：
```
[Info] LBR Language Server extension is now active
[Info] Found LSP server at: D:\...\server\ETBBS.Lsp.exe
[Info] LBR Language Server is ready
```

**错误日志示例**：
```
[Error] LSP server not found in: [...]
```

### 3. 检查服务器文件

```bash
cd vscode-lbr-extension
ls -la server/
```

**应该看到**：
```
ETBBS.Lsp.exe  (Windows) 或 ETBBS.Lsp.dll
ETBBS.dll
```

**如果文件不存在**：
```bash
# Windows
pwsh -File prepare-server.ps1

# Linux/macOS
./prepare-server.sh
```

### 4. 验证文件关联

在扩展开发主机中打开 .lbr 文件：

1. 右下角查看语言标识
2. 应该显示 **"LBR"** 或 **"lbr"**

**如果显示 "Plain Text"**：
1. 点击语言标识
2. 选择 "Configure File Association for '.lbr'..."
3. 选择 "LBR"

### 5. 手动测试服务器

在命令行测试 LSP 服务器：

```bash
cd vscode-lbr-extension/server
./ETBBS.Lsp.exe
```

输入：
```json
Content-Length: 123

{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
```

**如果服务器正常**：会返回 JSON 响应

**如果出错**：检查 .NET 运行时是否安装

---

## 常见错误和解决方案

### 错误 1: "LBR Language Server not found"

**原因**：服务器文件未准备

**解决**：
```bash
cd vscode-lbr-extension
pwsh -File prepare-server.ps1  # Windows
./prepare-server.sh            # Linux/macOS
```

### 错误 2: 无语法高亮

**原因 A**：文件未被识别为 LBR

**解决**：
- 确认文件扩展名是 `.lbr`
- 手动选择语言（右下角）

**原因 B**：语法文件未加载

**解决**：
- 重新编译：`npm run compile`
- 重启扩展开发主机

### 错误 3: LSP 功能不工作（无补全、无诊断）

**原因**：LSP 服务器未启动或崩溃

**诊断**：
1. 查看 Output 面板的错误
2. 查看 Debug Console 的错误

**常见原因**：
- .NET 8.0 运行时未安装
- 服务器权限问题
- 防火墙阻止

**解决**：
```bash
# 检查 .NET
dotnet --version  # 应该是 8.0.x

# 手动运行服务器查看错误
cd server
./ETBBS.Lsp.exe
```

### 错误 4: "Cannot find module 'vscode-languageclient'"

**原因**：依赖未安装

**解决**：
```bash
rm -rf node_modules package-lock.json
npm install
npm run compile
```

### 错误 5: 扩展在正式 VSCode 中不工作

**原因**：开发模式和安装模式路径不同

**解决**：
1. 检查打包是否包含 server/ 目录：
```bash
npm run package
unzip -l lbr-language-support-0.1.0.vsix | grep server
```

2. 确认 `.vscodeignore` 没有排除 server：
```
# .vscodeignore 不应该有这行：
# server/**
```

---

## 详细调试步骤

### 启用详细日志

1. 在扩展开发主机中：
   - File → Preferences → Settings
   - 搜索 `lbrLanguageServer.trace.server`
   - 设置为 `verbose`

2. 重新加载窗口：Ctrl+R

3. 查看 Output 面板的详细日志

### 使用 VSCode 调试器

1. 在 `extension.ts` 中设置断点
2. 按 F5 启动调试
3. 在扩展开发主机中触发功能
4. 断点会暂停，可查看变量

### 检查 LSP 通信

在 Output 面板启用 verbose 后，可以看到：

```
[Trace] Sending request 'initialize'
[Trace] Received response 'initialize'
[Trace] Sending notification 'textDocument/didOpen'
```

### 测试最小示例

创建最简单的 .lbr 文件：

```lbr
role "Test" id "test" {
  vars { "hp" = 100 }
}
```

如果这个都不能正常工作，说明基础配置有问题。

---

## 终极排查清单

- [ ] server/ 目录存在且包含 ETBBS.Lsp.exe 或 .dll
- [ ] `npm install` 成功
- [ ] `npm run compile` 无错误
- [ ] .lbr 文件被识别为 LBR 语言
- [ ] Debug Console 显示 "extension is now active"
- [ ] Output 面板有 "LBR Language Server" 选项
- [ ] .NET 8.0 运行时已安装
- [ ] 防火墙未阻止

---

## 获取帮助

如果以上都无法解决问题：

1. 收集信息：
   - Debug Console 的完整输出
   - Output 面板的完整日志
   - 操作系统版本
   - VSCode 版本
   - .NET 版本

2. 检查常见文件：
```bash
ls -la vscode-lbr-extension/
ls -la vscode-lbr-extension/server/
ls -la vscode-lbr-extension/client/out/
```

3. 尝试最小化复现：
   - 创建新的空 .lbr 文件
   - 在不同目录测试
   - 在新的 VSCode 窗口测试
