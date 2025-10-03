# 导出证书为 .cer 文件供用户安装
# 请以管理员身份运行此脚本

$thumbprint = "5177279CB78D45F254137A90628A84E56B8F1665"

Write-Host "正在导出证书..." -ForegroundColor Cyan

try {
    $cert = Get-ChildItem -Path "Cert:\CurrentUser\My\$thumbprint" -ErrorAction Stop
    Export-Certificate -Cert $cert -FilePath ".\LB_FATE_Certificate.cer" -Type CERT

    Write-Host "`n✓ 证书导出成功！" -ForegroundColor Green
    Write-Host "文件位置: $((Get-Item '.\LB_FATE_Certificate.cer').FullName)" -ForegroundColor Yellow
    Write-Host "`n将此 .cer 文件与 .msix 文件一起分发给用户。" -ForegroundColor Cyan
    Write-Host "用户需要先安装证书，然后才能安装应用。" -ForegroundColor Cyan

} catch {
    Write-Host "`n✗ 导出失败：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`n请确保：" -ForegroundColor Yellow
    Write-Host "1. 以管理员身份运行此脚本" -ForegroundColor Yellow
    Write-Host "2. 证书已正确创建（Thumbprint: $thumbprint）" -ForegroundColor Yellow
}

Read-Host "`n按 Enter 键退出"
