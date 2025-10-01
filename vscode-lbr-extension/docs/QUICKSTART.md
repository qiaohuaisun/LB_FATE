# 快速开始指南

## 🚀 5 分钟上手

### 步骤 1: 验证环境

```bash
cd vscode-lbr-extension
pwsh -File verify-setup.ps1
```

**应该看到**：
```
✓ Node.js found: vX.X.X
✓ .NET found: X.X.X
✓ ETBBS.Lsp.exe found
✓ node_modules found
✓ extension.js found
✓ example.lbr found

✓ All checks passed!
```

---

### 步骤 2: 安装和编译

```bash
# 如果验证未通过，运行这些命令
npm install          # 安装依赖
npm run compile      # 编译 TypeScript
```

---

### 步骤 3: 启动扩展

在 VSCode 中打开 `vscode-lbr-extension` 文件夹，然后：

**按 F5 键**

→ 会自动打开一个新的 VSCode 窗口（扩展开发主机）

---

### 步骤 4: 测试功能

在新窗口中：

1. **打开示例文件**
   - File → Open Folder → 选择 `examples` 文件夹
   - 打开 `example.lbr`

2. **检查语言标识**
   - 右下角应显示 **"LBR"**
   - 如果显示 "Plain Text"，点击它选择 "LBR"

3. **测试功能**（按顺序尝试）

---

## ✨ 功能测试清单

### ✅ 1. 语法高亮

打开文件后应该自动生效：

```lbr
role "Test" id "test" {  // 蓝色关键字
  vars { "hp" = 100 }    // 橙色字符串，绿色数字
}
```

### ✅ 2. 智能补全

输入 `de` 然后按 **Ctrl+Space**：

```lbr
skill "Test" {
  de|    ← 光标在这里
}
```

应该显示：
- `deal`
- `description`

### ✅ 3. 实时诊断

输入错误代码：

```lbr
skill "Error" {
  range 3
  min_range 5    ← 红色波浪线
}
```

悬停应显示：`min_range (5) exceeds range (3)`

### ✅ 4. 悬停文档

悬停在 `targeting` 关键字上：

```lbr
targeting enemies
//        ↑ 悬停这里
```

应显示文档气泡

### ✅ 5. 快速修复

在错误代码处按 **Ctrl+.**：

应显示修复建议

### ✅ 6. 代码格式化

按 **Shift+Alt+F**：

```lbr
// 格式化前
role "Test" id "t" {
vars{"hp"=100;}
}

// 格式化后
role "Test" id "t" {
  vars { "hp" = 100; }
}
```

### ✅ 7. 符号搜索

按 **Ctrl+T**，输入技能名：

应该找到并高亮显示

---

## 🎯 常见问题

### Q: 按 F5 没反应？

**A**: 确保：
1. 在 VSCode 中打开的是 `vscode-lbr-extension` 文件夹
2. 已运行 `npm run compile`
3. 查看 Debug Console (Ctrl+Shift+Y) 的错误信息

### Q: 无语法高亮？

**A**:
1. 检查右下角语言标识是否为 "LBR"
2. 如果不是，点击选择 "LBR"

### Q: 无自动补全？

**A**:
1. 查看 Output 面板 → LBR Language Server
2. 应该看到 "LBR Language Server is ready"
3. 如果没有，检查服务器文件是否存在：
   ```bash
   ls server/ETBBS.Lsp.exe
   ```

### Q: 服务器未找到？

**A**:
```bash
pwsh -File prepare-server.ps1
```

---

## 📖 下一步

- 阅读 [USAGE.md](USAGE.md) 了解详细功能
- 查看 [DEBUG.md](../DEBUG.md) 进行故障排除
- 参考 [BUILD.md](BUILD.md) 了解如何打包

---

## 🏁 成功标志

如果你完成了上面的所有测试，并且都正常工作，恭喜！🎉

扩展已经可以使用了。

**开始编写你的角色定义吧！**
