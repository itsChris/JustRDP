param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,
    [switch]$Publish,
    [switch]$SelfContained,
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$Solution = Join-Path $ProjectRoot "JustRDP.slnx"
$EntryProject = Join-Path $ProjectRoot "src\JustRDP.Presentation\JustRDP.Presentation.csproj"
$OutputDir = Join-Path $ProjectRoot "build\$Configuration"

Write-Host "=== JustRDP Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host ""

# Clean
if ($Clean) {
    Write-Host "Cleaning..." -ForegroundColor Yellow
    dotnet clean $Solution -c $Configuration --nologo -v q
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    Write-Host "Clean complete." -ForegroundColor Green
    Write-Host ""
}

# Restore
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore $Solution --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "Restore failed." -ForegroundColor Red; exit 1 }
Write-Host "Restore complete." -ForegroundColor Green
Write-Host ""

# Build
Write-Host "Building ($Configuration)..." -ForegroundColor Yellow
dotnet build $Solution -c $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." -ForegroundColor Red; exit 1 }
Write-Host "Build succeeded." -ForegroundColor Green
Write-Host ""

# Publish
if ($Publish) {
    Write-Host "Publishing to $OutputDir..." -ForegroundColor Yellow
    if ($SelfContained) {
        Write-Host "Mode: self-contained (no .NET runtime required on target)" -ForegroundColor Cyan
        dotnet publish $EntryProject -c $Configuration -o $OutputDir --self-contained -r win-x64 --nologo
    } else {
        dotnet publish $EntryProject -c $Configuration -o $OutputDir --no-build --nologo
    }
    if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." -ForegroundColor Red; exit 1 }
    Write-Host "Published to $OutputDir" -ForegroundColor Green
    Write-Host ""
}

# Run
if ($Run) {
    Write-Host "Starting JustRDP..." -ForegroundColor Yellow
    dotnet run --project $EntryProject -c $Configuration --no-build
}
