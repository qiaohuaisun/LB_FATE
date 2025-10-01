# PowerShell script to prepare the LSP server for distribution

Write-Host "Preparing ETBBS.Lsp server for VSCode extension..." -ForegroundColor Green

# Create server directory
$serverDir = "server"
if (Test-Path $serverDir) {
    Remove-Item -Recurse -Force $serverDir
}
New-Item -ItemType Directory -Path $serverDir | Out-Null

# Build the LSP server in Release mode
Write-Host "Building ETBBS.Lsp..." -ForegroundColor Yellow
Set-Location ..
dotnet publish ETBBS.Lsp/ETBBS.Lsp.csproj -c Release -o vscode-lbr-extension/server

Set-Location vscode-lbr-extension

Write-Host "Server prepared successfully!" -ForegroundColor Green
Write-Host "Server location: $serverDir" -ForegroundColor Cyan

# List server files
Get-ChildItem $serverDir | Format-Table Name, Length

pause
