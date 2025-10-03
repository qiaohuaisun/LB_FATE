# LB_FATE Windows MSIX 部署指南

## 生成的包文件

**正式版MSIX包位置：**
```
LB_FATE.Mobile\bin\Release\net10.0-windows10.0.19041.0\win-x64\AppPackages\
```

**包信息：**
- 架构：x64
- 签名证书：LB_FATE Publisher (自签名)
- 证书Thumbprint：5177279CB78D45F254137A90628A84E56B8F1665

---

## 关于 "_Test" 标记

使用自签名证书时，包名会包含 `_Test` 标记。这是 Windows 的正常行为，表示该包：
- ✅ 用于**旁加载**（sideloading）分发
- ✅ 功能完全正常，可以正式使用
- ⚠️ 需要先安装证书才能安装应用

**要去除 "_Test" 标记，只有两种方法：**
1. 购买商业代码签名证书（约 $100-500/年）
2. 通过 Microsoft Store 发布（微软自动签名）

---

## 用户安装步骤

### 步骤 1：安装证书

用户需要先安装你的自签名证书才能安装应用。

**导出证书（.cer格式）：**

在项目根目录运行 `export_certificate.ps1` 脚本（需要管理员权限）：

```powershell
.\export_certificate.ps1
```

这将生成 `LB_FATE_Certificate.cer` 文件用于分发。

**用户安装证书：**

1. 双击 `LB_FATE_Certificate.cer` 文件
2. 点击 **安装证书**
3. 选择 **当前用户** 或 **本地计算机**
4. 选择 **将所有的证书都放入下列存储**
5. 点击 **浏览**，选择 **受信任的根证书颁发机构**
6. 点击 **确定** → **下一步** → **完成**
7. 确认安全警告

### 步骤 2：安装应用

1. 双击 `.msix` 文件
2. 点击 **安装**
3. 等待安装完成

---

## 分发包内容

分发给用户时，需要包含：

```
📦 LB_FATE_Windows_Release/
├── LB_FATE.Mobile_x.x.x.x_x64.msix  # 应用安装包
├── LB_FATE_Certificate.cer          # 证书文件
└── 安装说明.txt                      # 简单的安装步骤说明
```

**安装说明.txt 示例：**

```
LB_FATE 安装步骤

1. 双击运行 "LB_FATE_Certificate.cer"
   - 点击"安装证书"
   - 选择"当前用户"
   - 选择"受信任的根证书颁发机构"
   - 完成证书安装

2. 双击运行 ".msix" 文件
   - 点击"安装"
   - 等待完成

3. 在开始菜单中找到 "LB_FATE" 启动应用

注意：首次安装需要安装证书，后续更新无需重复此步骤。
```

---

## 创建新证书（可选）

如果需要创建新的自签名证书，以**管理员身份**打开 PowerShell 并运行：

```powershell
# 创建自签名证书
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=LB_FATE Publisher" `
    -KeyUsage DigitalSignature `
    -FriendlyName "LB_FATE Release Certificate" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# 显示证书信息
Write-Host "证书创建成功！" -ForegroundColor Green
Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor Yellow

# 导出证书为PFX文件
$password = ConvertTo-SecureString -String "YOUR_PASSWORD_HERE" -Force -AsPlainText
Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" `
    -FilePath ".\LB_FATE_Certificate.pfx" -Password $password

Write-Host "证书已导出到: LB_FATE_Certificate.pfx" -ForegroundColor Green
Write-Host "请更新 .csproj 中的 PackageCertificateThumbprint" -ForegroundColor Yellow
```

然后在 `LB_FATE.Mobile.csproj` 中更新：
```xml
<PackageCertificateThumbprint>YOUR_NEW_THUMBPRINT</PackageCertificateThumbprint>
```

---

## 更新应用版本

修改 `.csproj` 文件中的版本号：

```xml
<ApplicationDisplayVersion>1.1</ApplicationDisplayVersion>
<ApplicationVersion>2</ApplicationVersion>
```

然后重新打包：

```bash
dotnet publish -f net10.0-windows10.0.19041.0 -c Release
```

---

## 升级到正式证书（可选）

如果未来需要升级到商业证书：

1. 购买代码签名证书（DigiCert, Sectigo 等）
2. 将 `.pfx` 文件放到项目目录
3. 更新 `.csproj`：
   ```xml
   <PackageCertificateKeyFile>YourCommercialCert.pfx</PackageCertificateKeyFile>
   <PackageCertificateThumbprint>YOUR_NEW_THUMBPRINT</PackageCertificateThumbprint>
   ```
4. 重新打包即可去除 "_Test" 标记

---

## 常见问题

**Q: 为什么必须安装证书？**
A: Windows 只允许安装由受信任来源签名的应用。自签名证书需要手动添加到受信任列表。

**Q: 证书安全吗？**
A: 自签名证书只是用于标识开发者身份。用户需要信任你的证书才能安装应用。

**Q: 可以跳过证书安装吗？**
A: 不可以。这是 Windows 的安全机制。

**Q: 更新应用时需要重新安装证书吗？**
A: 不需要。证书只需安装一次，后续更新自动识别。

---

## 证书信息

**当前证书：**
- 发布者：CN=LB_FATE Publisher
- Thumbprint：5177279CB78D45F254137A90628A84E56B8F1665
- 用途：代码签名（自签名）

**警告：** 请妥善保管 `.pfx` 文件和密码，不要分发给用户。只分发 `.cer` 文件。
