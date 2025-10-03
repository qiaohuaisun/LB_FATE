# 证书诊断脚本
# 帮助诊断 MSIX 安装问题

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "    MSIX 安装问题诊断工具" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

$thumbprint = "5177279CB78D45F254137A90628A84E56B8F1665"

# 检查 1: 证书是否在个人存储中
Write-Host "`n[1] 检查证书是否在个人存储中..." -ForegroundColor Yellow
try {
    $cert = Get-ChildItem -Path "Cert:\CurrentUser\My\$thumbprint" -ErrorAction Stop
    Write-Host "    ✓ 证书存在于个人存储" -ForegroundColor Green
    Write-Host "      主题: $($cert.Subject)" -ForegroundColor Gray
    Write-Host "      有效期至: $($cert.NotAfter)" -ForegroundColor Gray
} catch {
    Write-Host "    ✗ 证书不在个人存储中" -ForegroundColor Red
    Write-Host "      需要重新创建证书！" -ForegroundColor Yellow
}

# 检查 2: 证书是否在受信任的根证书存储中
Write-Host "`n[2] 检查证书是否在受信任的根证书存储中..." -ForegroundColor Yellow
try {
    $rootCert = Get-ChildItem -Path "Cert:\CurrentUser\Root\$thumbprint" -ErrorAction Stop
    Write-Host "    ✓ 证书已安装到受信任的根证书" -ForegroundColor Green
} catch {
    Write-Host "    ✗ 证书未安装到受信任的根证书" -ForegroundColor Red
    Write-Host "      这是导致 MSIX 无法安装的最常见原因！" -ForegroundColor Yellow
}

# 检查 3: .cer 文件是否存在
Write-Host "`n[3] 检查 .cer 文件是否存在..." -ForegroundColor Yellow
if (Test-Path ".\LB_FATE_Certificate.cer") {
    Write-Host "    ✓ .cer 文件存在" -ForegroundColor Green
} else {
    Write-Host "    ✗ .cer 文件不存在" -ForegroundColor Red
    Write-Host "      需要导出证书文件供用户安装" -ForegroundColor Yellow
}

# 检查 4: Windows 开发者模式
Write-Host "`n[4] 检查 Windows 开发者模式..." -ForegroundColor Yellow
$devMode = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -ErrorAction SilentlyContinue
if ($devMode.AllowDevelopmentWithoutDevLicense -eq 1) {
    Write-Host "    ✓ 开发者模式已启用" -ForegroundColor Green
} else {
    Write-Host "    ⚠ 开发者模式未启用（旁加载需要）" -ForegroundColor Yellow
}

# 检查 5: 最近生成的 MSIX 包
Write-Host "`n[5] 检查最近的 MSIX 包..." -ForegroundColor Yellow
$msixFiles = Get-ChildItem -Path ".\bin\Release" -Filter "*.msix" -Recurse -ErrorAction SilentlyContinue |
             Where-Object { $_.Name -notlike "*Dependencies*" } |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1

if ($msixFiles) {
    Write-Host "    ✓ 找到 MSIX 包: $($msixFiles.Name)" -ForegroundColor Green
    Write-Host "      路径: $($msixFiles.FullName)" -ForegroundColor Gray
    Write-Host "      大小: $([math]::Round($msixFiles.Length / 1MB, 2)) MB" -ForegroundColor Gray
    Write-Host "      生成时间: $($msixFiles.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "    ✗ 未找到 MSIX 包" -ForegroundColor Red
    Write-Host "      请先运行: dotnet publish -f net10.0-windows10.0.19041.0 -c Release" -ForegroundColor Yellow
}

# 总结和建议
Write-Host "`n======================================" -ForegroundColor Cyan
Write-Host "    诊断总结和解决方案" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

$cert = Get-ChildItem -Path "Cert:\CurrentUser\My\$thumbprint" -ErrorAction SilentlyContinue
$rootCert = Get-ChildItem -Path "Cert:\CurrentUser\Root\$thumbprint" -ErrorAction SilentlyContinue

if (-not $cert) {
    Write-Host "`n[问题] 证书不存在" -ForegroundColor Red
    Write-Host "解决方案: 运行以下命令重新创建证书" -ForegroundColor Yellow
    Write-Host "  参考: CERTIFICATE_README.md" -ForegroundColor Gray
}
elseif (-not $rootCert) {
    Write-Host "`n[问题] 证书未安装到受信任的根证书存储" -ForegroundColor Red
    Write-Host "解决方案:" -ForegroundColor Yellow
    Write-Host "  1. 运行 .\export_certificate.ps1 导出 .cer 文件" -ForegroundColor White
    Write-Host "  2. 双击 LB_FATE_Certificate.cer" -ForegroundColor White
    Write-Host "  3. 点击 '安装证书'" -ForegroundColor White
    Write-Host "  4. 选择 '当前用户'" -ForegroundColor White
    Write-Host "  5. 选择 '将所有证书都放入下列存储'" -ForegroundColor White
    Write-Host "  6. 浏览并选择 '受信任的根证书颁发机构'" -ForegroundColor White
    Write-Host "  7. 完成安装" -ForegroundColor White
}
else {
    Write-Host "`n[状态] 证书配置正确！" -ForegroundColor Green
    if ($msixFiles) {
        Write-Host "可以尝试安装 MSIX 包了" -ForegroundColor Green
    }
}

Write-Host "`n" -NoNewline
Read-Host "按 Enter 键退出"
