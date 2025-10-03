# 项目清理脚本
# 清理构建产物和临时文件

Write-Host "开始清理项目..." -ForegroundColor Cyan

# 清理构建产物
Write-Host "`n清理构建产物 (bin, obj)..." -ForegroundColor Yellow
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# 清理 NuGet 缓存
Write-Host "清理 NuGet 包缓存..." -ForegroundColor Yellow
dotnet nuget locals all --clear

# 清理临时文件
Write-Host "清理临时文件..." -ForegroundColor Yellow
Get-ChildItem -Path . -Include *.tmp,*.log,*.bak -Recurse -File | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "`n✓ 清理完成！" -ForegroundColor Green
Write-Host "`n下次构建前会自动还原依赖项。" -ForegroundColor Cyan
