# MSIX 安装故障排除指南

如果安装了证书后仍然无法安装 MSIX 包，请按照以下步骤诊断和解决问题。

---

## 🔍 快速诊断

### 步骤 1: 运行诊断脚本

```powershell
cd LB_FATE.Mobile
.\diagnose_cert.ps1
```

这个脚本会自动检查：
- ✅ 证书是否存在
- ✅ 证书是否在正确的存储位置
- ✅ Windows 开发者模式状态
- ✅ MSIX 包是否存在

---

## 🚨 常见问题和解决方案

### 问题 1: 证书安装位置不正确 ⭐ 最常见

**症状：**
- 双击 .cer 文件安装后，MSIX 仍然无法安装
- 提示：发布者不受信任

**原因：**
证书必须安装到 **"受信任的根证书颁发机构"**，而不是其他位置。

**解决方案：**

1. **删除之前安装的证书（如果有）**
   - 按 `Win + R`，输入 `certmgr.msc`，回车
   - 展开 **个人** → **证书**
   - 找到 "LB_FATE Publisher" 证书，右键删除
   - 展开 **其他人** → **证书**
   - 如果有同名证书，也删除

2. **重新安装到正确位置**
   ```
   1. 双击 LB_FATE_Certificate.cer
   2. 点击 "安装证书"
   3. 选择 "当前用户"
   4. 选择 "将所有的证书都放入下列存储"
   5. 点击 "浏览"
   6. 选择 "受信任的根证书颁发机构" ← 这一步最关键！
   7. 点击 "确定" → "下一步" → "完成"
   8. 确认安全警告
   ```

3. **验证安装**
   ```powershell
   # 运行此命令检查
   Get-ChildItem Cert:\CurrentUser\Root | Where-Object { $_.Subject -like "*LB_FATE*" }
   ```

   应该能看到证书信息。

---

### 问题 2: 证书未导出或已过期

**症状：**
- 没有 .cer 文件
- 证书过期

**解决方案：**

1. **导出证书**（以管理员身份运行）：
   ```powershell
   cd LB_FATE.Mobile
   .\export_certificate.ps1
   ```

2. **如果证书不存在或已过期，重新创建**：
   参考 `CERTIFICATE_README.md` 中的步骤

---

### 问题 3: Windows 开发者模式未启用

**症状：**
- 提示：需要开发者许可证
- 提示：无法安装此应用包

**解决方案：**

**方法 A: 启用开发者模式（推荐）**

1. 打开 **设置** → **更新和安全** → **开发者选项**
2. 选择 **开发人员模式**
3. 确认更改
4. 重启电脑（可能需要）

**方法 B: 启用旁加载（仅安装测试应用）**

1. 打开 **设置** → **更新和安全** → **开发者选项**
2. 选择 **旁加载应用**

---

### 问题 4: 证书和 MSIX 包不匹配

**症状：**
- 证书安装成功但 MSIX 仍无法安装
- 提示：签名验证失败

**解决方案：**

1. **检查证书 Thumbprint**
   ```powershell
   # 查看当前配置的证书
   Get-Content LB_FATE.Mobile.csproj | Select-String "PackageCertificateThumbprint"

   # 应该显示: 5177279CB78D45F254137A90628A84E56B8F1665
   ```

2. **重新构建 MSIX 包**
   ```bash
   dotnet clean
   dotnet publish -f net10.0-windows10.0.19041.0 -c Release
   ```

---

### 问题 5: MSIX 包损坏或不完整

**症状：**
- 安装时出现意外错误
- 错误代码：0x80073CF3, 0x80070002

**解决方案：**

1. **清理并重新构建**
   ```bash
   cd LB_FATE.Mobile
   dotnet clean
   rm -rf bin obj
   dotnet restore
   dotnet publish -f net10.0-windows10.0.19041.0 -c Release
   ```

2. **检查包完整性**
   - 确保 MSIX 文件大小正常（约 20-30 MB）
   - 检查文件没有在构建时被中断

---

## 📋 完整安装步骤（推荐流程）

### 对于开发者自己安装：

1. **启用开发者模式**
   ```
   设置 → 更新和安全 → 开发者选项 → 开发人员模式
   ```

2. **确认证书存在并导出**
   ```powershell
   cd LB_FATE.Mobile
   .\export_certificate.ps1
   ```

3. **安装证书到受信任的根证书**
   ```
   双击 LB_FATE_Certificate.cer
   → 安装证书
   → 当前用户
   → 受信任的根证书颁发机构
   ```

4. **构建 MSIX 包**
   ```bash
   dotnet publish -f net10.0-windows10.0.19041.0 -c Release
   ```

5. **安装 MSIX**
   ```
   双击生成的 .msix 文件
   ```

### 对于分发给其他用户：

1. **准备文件**
   ```
   📦 分发包/
   ├── LB_FATE.Mobile_x.x.x.x_x64.msix
   ├── LB_FATE_Certificate.cer
   └── 安装说明.txt
   ```

2. **用户步骤**
   ```
   1. 双击 LB_FATE_Certificate.cer 安装证书
      （确保安装到"受信任的根证书颁发机构"）
   2. 双击 .msix 文件安装应用
   ```

---

## 🔧 高级诊断命令

```powershell
# 检查证书是否在受信任的根证书存储中
Get-ChildItem Cert:\CurrentUser\Root |
  Where-Object { $_.Subject -like "*LB_FATE*" } |
  Format-List Subject, Thumbprint, NotAfter

# 检查 MSIX 包签名
Get-AppxPackage -Name "*LB_FATE*" | Format-List

# 查看详细错误日志
Get-AppxLog | Where-Object { $_.Message -like "*LB_FATE*" } |
  Select-Object TimeCreated, Message | Format-List
```

---

## ❓ 仍然无法解决？

### 检查清单：

- [ ] 证书已安装到 **受信任的根证书颁发机构**（不是"个人"或"其他人"）
- [ ] Windows 开发者模式已启用
- [ ] MSIX 包是最新构建的（与证书匹配）
- [ ] 防病毒软件没有阻止安装
- [ ] Windows 更新已安装最新补丁
- [ ] 使用的是 Windows 10 版本 17763 或更高

### 终极解决方案：

如果以上都无效，尝试：

1. **使用 PowerShell 强制安装**
   ```powershell
   Add-AppxPackage -Path "path\to\your.msix" -ForceApplicationShutdown
   ```

2. **检查 Windows 事件查看器**
   ```
   运行 → eventvwr.msc
   → 应用程序和服务日志
   → Microsoft → Windows → AppxPackaging → Operational
   ```

3. **重新创建全新证书**
   - 删除所有旧证书
   - 按照 `CERTIFICATE_README.md` 创建新证书
   - 更新 .csproj 中的 Thumbprint
   - 重新构建

---

## 📚 相关文档

- [CERTIFICATE_README.md](CERTIFICATE_README.md) - 证书创建和管理
- [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md) - 完整部署指南
- [diagnose_cert.ps1](diagnose_cert.ps1) - 自动诊断脚本

---

**提示：** 90% 的安装问题都是因为证书没有安装到"受信任的根证书颁发机构"。请仔细检查这一步！
