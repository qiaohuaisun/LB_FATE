# 证书文件说明

## ⚠️ 重要提示

`LB_FATE_Certificate.pfx` 文件未包含在 Git 仓库中（已在 `.gitignore` 中忽略）。

## 如果证书文件丢失

如果您的 `LB_FATE_Certificate.pfx` 文件丢失或不存在，您有两个选择：

### 选项 1：重新创建证书（推荐）

以**管理员身份**运行以下 PowerShell 命令：

```powershell
# 创建新证书
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=LB_FATE Publisher" `
    -KeyUsage DigitalSignature `
    -FriendlyName "LB_FATE Release Certificate" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# 导出为 PFX
$password = ConvertTo-SecureString -String "LB_FATE_2025" -Force -AsPlainText
Export-PfxCertificate `
    -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" `
    -FilePath "LB_FATE_Certificate.pfx" `
    -Password $password

# 显示新的 Thumbprint
Write-Host "新证书 Thumbprint: $($cert.Thumbprint)" -ForegroundColor Yellow
```

**然后更新 `LB_FATE.Mobile.csproj`：**

```xml
<PackageCertificateThumbprint>YOUR_NEW_THUMBPRINT_HERE</PackageCertificateThumbprint>
```

### 选项 2：使用现有证书（如果有备份）

如果您有 `.pfx` 文件的备份，请：

1. 将备份文件复制到此目录
2. 确保文件名为 `LB_FATE_Certificate.pfx`
3. 确认 `.csproj` 中的 Thumbprint 匹配

## 当前配置

`.csproj` 中配置的证书 Thumbprint：
```
5177279CB78D45F254137A90628A84E56B8F1665
```

**验证证书是否存在：**

```powershell
# 检查证书是否在存储中
Get-ChildItem Cert:\CurrentUser\My\5177279CB78D45F254137A90628A84E56B8F1665
```

如果命令返回证书信息，说明证书仍在系统中，您可以重新导出：

```powershell
$cert = Get-ChildItem Cert:\CurrentUser\My\5177279CB78D45F254137A90628A84E56B8F1665
$password = ConvertTo-SecureString -String "LB_FATE_2025" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "LB_FATE_Certificate.pfx" -Password $password
```

## 密码

默认密码：`LB_FATE_2025`

**重要：** 不要将密码提交到版本控制系统。
