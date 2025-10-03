# 安全配置设置指南

本指南帮助你安全地配置项目，避免泄露敏感信息。

## 🔐 为什么需要安全配置？

项目需要以下敏感信息来构建发布版本：
- **Android 签名密钥密码** - 用于签名 APK/AAB
- **Windows 证书** - 用于签名 MSIX 包

这些信息**不应该**提交到版本控制系统（Git）。

---

## ⚙️ 配置步骤

### 步骤 1：创建本地配置文件

从模板复制创建实际配置文件：

```bash
# 复制 .user 文件模板
cd LB_FATE.Mobile
copy LB_FATE.Mobile.csproj.user.example LB_FATE.Mobile.csproj.user
```

**或者**（可选）使用环境变量：

```bash
# 复制 .env 文件模板
copy .env.example .env
```

### 步骤 2：填入实际密码

编辑 `LB_FATE.Mobile/LB_FATE.Mobile.csproj.user` 文件：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <!-- 替换为你的实际密码 -->
    <AndroidSigningPassword>YOUR_ACTUAL_PASSWORD_HERE</AndroidSigningPassword>
  </PropertyGroup>
</Project>
```

### 步骤 3：验证 .gitignore 配置

确保以下文件已被 Git 忽略：

```gitignore
# 已配置在 .gitignore 中：
*.user          # 包含 .csproj.user 文件
*.pfx           # Windows 证书
*.keystore      # Android 密钥库
.env            # 环境变量文件
```

验证命令：

```bash
git status
# 不应该看到 .user 或 .pfx 文件
```

---

## ✅ 验证配置

### 测试 Android 构建

```bash
dotnet publish LB_FATE.Mobile -f net10.0-android -c Release
```

如果配置正确，应该能成功生成签名的 APK 文件。

### 测试 Windows 构建

```bash
dotnet publish LB_FATE.Mobile -f net10.0-windows10.0.19041.0 -c Release
```

如果证书配置正确，应该能成功生成 MSIX 包。

---

## 🚨 安全检查清单

在提交代码前，请检查：

- [ ] **.user 文件未提交** - 运行 `git status` 确认
- [ ] **.pfx 文件未提交** - 证书文件应该只在本地
- [ ] **.keystore 文件安全** - 备份到安全位置
- [ ] **密码不在代码中** - 检查 `.csproj` 文件没有硬编码密码
- [ ] **.gitignore 正确配置** - 确保敏感文件被忽略

---

## 🔄 团队协作

### 新成员设置

1. 克隆仓库后，从团队获取以下文件（通过安全渠道）：
   - `LB_FATE_Certificate.pfx` (Windows 证书)
   - `linbei.keystore` (Android 密钥库)
   - 密码信息

2. 按照上述步骤配置本地环境

3. **永远不要**通过 Git、邮件或聊天工具发送这些文件

### CI/CD 配置

对于持续集成环境：

1. 使用 **加密的环境变量** 存储密码
2. 证书文件使用 **安全存储**（如 GitHub Secrets, Azure Key Vault）
3. 在构建脚本中动态注入配置

示例（GitHub Actions）：

```yaml
env:
  ANDROID_SIGNING_PASSWORD: ${{ secrets.ANDROID_SIGNING_PASSWORD }}
```

---

## 📖 相关文档

- [SECURITY.md](SECURITY.md) - 完整安全最佳实践
- [LB_FATE.Mobile/DEPLOYMENT_GUIDE.md](LB_FATE.Mobile/DEPLOYMENT_GUIDE.md) - Windows 部署指南
- [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) - 项目结构说明

---

## 🆘 常见问题

### Q: 我不小心提交了密码怎么办？

A: 立即执行以下操作：
1. 更改所有相关密码/密钥
2. 使用 `git filter-branch` 或 BFG Repo-Cleaner 清理 Git 历史
3. 强制推送到远程仓库
4. 通知所有团队成员拉取新历史

### Q: 可以把密码写在注释里吗？

A: **绝对不可以**。即使在注释中，密码也会被提交到版本历史中。

### Q: .user 文件丢失了怎么办？

A: 从 `.user.example` 模板重新创建，填入你的密码即可。

### Q: 如何备份密钥文件？

A:
1. 将 `.pfx` 和 `.keystore` 文件备份到**加密的**安全位置
2. 密码单独存储在密码管理器中（如 1Password, Bitwarden）
3. **不要**将密钥文件放在云同步文件夹中

---

**重要提示**：安全配置是持续的过程，而不是一次性任务。定期审查并更新安全措施。
