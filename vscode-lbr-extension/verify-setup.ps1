# 验证扩展设置脚本

# 设置控制台编码为 UTF-8
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['Out-File:Encoding'] = 'utf8'

# 设置代码页为 UTF-8
chcp 65001 > $null

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "LBR VSCode Extension Setup Verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$allGood = $true

# 1. 检查 Node.js
Write-Host "[1/6] Checking Node.js..." -ForegroundColor Yellow
try {
    $nodeVersion = node --version
    Write-Host "  [OK] Node.js found: $nodeVersion" -ForegroundColor Green
} catch {
    Write-Host "  [FAIL] Node.js not found!" -ForegroundColor Red
    $allGood = $false
}

# 2. 检查 .NET
Write-Host "[2/6] Checking .NET Runtime..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "  [OK] .NET found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "  [FAIL] .NET 8.0 not found!" -ForegroundColor Red
    $allGood = $false
}

# 3. 检查 server 目录
Write-Host "[3/6] Checking LSP server files..." -ForegroundColor Yellow
if (Test-Path "server/ETBBS.Lsp.exe") {
    Write-Host "  [OK] ETBBS.Lsp.exe found" -ForegroundColor Green
} elseif (Test-Path "server/ETBBS.Lsp.dll") {
    Write-Host "  [OK] ETBBS.Lsp.dll found" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] LSP server not found in server/" -ForegroundColor Red
    Write-Host "  --> Run: pwsh -File prepare-server.ps1" -ForegroundColor Yellow
    $allGood = $false
}

# 4. 检查 node_modules
Write-Host "[4/6] Checking npm dependencies..." -ForegroundColor Yellow
if (Test-Path "node_modules") {
    Write-Host "  [OK] node_modules found" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] node_modules not found" -ForegroundColor Red
    Write-Host "  --> Run: npm install" -ForegroundColor Yellow
    $allGood = $false
}

# 5. 检查编译输出
Write-Host "[5/6] Checking compiled output..." -ForegroundColor Yellow
if (Test-Path "client/out/extension.js") {
    Write-Host "  [OK] extension.js found" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Compiled output not found" -ForegroundColor Red
    Write-Host "  --> Run: npm run compile" -ForegroundColor Yellow
    $allGood = $false
}

# 6. 检查示例文件
Write-Host "[6/6] Checking example files..." -ForegroundColor Yellow
if (Test-Path "examples/example.lbr") {
    Write-Host "  [OK] example.lbr found" -ForegroundColor Green
} else {
    Write-Host "  [WARN] No example.lbr (optional)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

if ($allGood) {
    Write-Host "[SUCCESS] All checks passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Open this folder in VSCode" -ForegroundColor White
    Write-Host "  2. Press F5 to launch Extension Development Host" -ForegroundColor White
    Write-Host "  3. Open examples/example.lbr in the new window" -ForegroundColor White
} else {
    Write-Host "[ERROR] Some checks failed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please fix the issues above and run this script again." -ForegroundColor Yellow
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

pause
