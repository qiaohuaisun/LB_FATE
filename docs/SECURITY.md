# 安全说明

## ⚠️ 重要安全提醒

项目中存在以下安全敏感信息：

### 1. Android 签名密钥

**文件：** `LB_FATE.Mobile/LB_FATE.Mobile.csproj`

当前配置包含硬编码的密钥库密码：
- KeyStore: `linbei.keystore`
- 密码：硬编码在 `.csproj` 文件中

**建议改进：**

```xml
<!-- 移除硬编码密码，改为使用环境变量或 .user 文件 -->
<PropertyGroup Condition="'$(Configuration)|$(TargetFramework)|$(Platform)'=='Release|net10.0-android|AnyCPU'">
  <AndroidPackageFormat>apk</AndroidPackageFormat>
  <AndroidKeyStore>True</AndroidKeyStore>
  <AndroidSigningStorePass>$(AndroidSigningPassword)</AndroidSigningStorePass>
  <AndroidSigningKeyAlias>lbfate</AndroidSigningKeyAlias>
  <AndroidSigningKeyPass>$(AndroidSigningPassword)</AndroidSigningKeyPass>
</PropertyGroup>
```

然后在本地创建 `LB_FATE.Mobile.csproj.user` 文件（已在 .gitignore 中忽略）：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <AndroidSigningPassword>YOUR_PASSWORD_HERE</AndroidSigningPassword>
  </PropertyGroup>
</Project>
```

### 2. Windows 签名证书

**文件：** `LB_FATE.Mobile/LB_FATE_Certificate.pfx`

- 证书密码：`LB_FATE_2025`（在 `DEPLOYMENT_GUIDE.md` 中）
- 建议：不要将 `.pfx` 文件提交到公共仓库

**当前状态：**
- ✅ `.pfx` 文件已在 `.gitignore` 中忽略
- ✅ 证书仅用于开发/测试环境

### 3. 最佳实践

1. **不要提交敏感文件到公共仓库：**
   - `*.pfx` (Windows 证书)
   - `*.keystore` (Android 密钥库)
   - 包含密码的配置文件

2. **使用环境变量或用户配置文件：**
   - CI/CD 环境：使用加密的环境变量
   - 本地开发：使用 `.user` 文件（已被 .gitignore 忽略）

3. **定期更换密钥：**
   - 开发环境密钥应定期更换
   - 生产环境密钥使用强密码并安全存储

## 当前项目配置

由于这是一个本地/私有项目，当前配置可以接受。但如果要公开发布或团队协作，建议：

1. 移除 `.csproj` 中的硬编码密码
2. 将 `linbei.keystore` 添加到 `.gitignore`（如果尚未添加）
3. 使用安全的密钥管理方案

## 检查清单

在提交代码前，请检查：

- [ ] 没有硬编码的密码
- [ ] `.pfx` 和 `.keystore` 文件已被忽略
- [ ] 敏感配置使用环境变量或 `.user` 文件
- [ ] `.gitignore` 配置正确

---

**注意：** 如果密码已经被提交到 git 历史中，需要使用 `git filter-branch` 或 BFG Repo-Cleaner 清除历史记录。
