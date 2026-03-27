$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$EntryProject = Join-Path $ProjectRoot "src\JustRDP.Presentation\JustRDP.Presentation.csproj"
$OutputDir = Join-Path $ProjectRoot "build\SelfContained"

Write-Host "=== JustRDP Self-Contained Publish ===" -ForegroundColor Cyan
Write-Host ""

# Clean output
if (Test-Path $OutputDir) {
    Write-Host "Cleaning previous output..." -ForegroundColor Yellow
    Remove-Item $OutputDir -Recurse -Force
}

# Publish
Write-Host "Publishing (self-contained, win-x64)..." -ForegroundColor Yellow
dotnet publish $EntryProject -c Release -o $OutputDir --self-contained -r win-x64 --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "Published to $OutputDir" -ForegroundColor Green

# Zip with timestamp
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipName = "JustRDP-$timestamp.zip"
$zipPath = Join-Path $ProjectRoot "build\$zipName"

Write-Host "Creating $zipName..." -ForegroundColor Yellow
Compress-Archive -Path "$OutputDir\*" -DestinationPath $zipPath -Force
Write-Host "Zipped to $zipPath" -ForegroundColor Green
Write-Host "No .NET runtime required on target machine." -ForegroundColor Cyan
