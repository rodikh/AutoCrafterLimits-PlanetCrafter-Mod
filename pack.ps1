# AutoCrafterLimits Nexus Mods Pack Script
# Builds the mod and creates a versioned zip for distribution.

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$DllPath = Join-Path $ProjectDir "bin\Release\netstandard2.1\AutoCrafterLimits.dll"
$PluginCs = Join-Path $ProjectDir "Plugin.cs"

Write-Host "AutoCrafterLimits Pack (Nexus Mods)" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Parse version from Plugin.cs
$versionMatch = Select-String -Path $PluginCs -Pattern 'PluginVersion\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"' | Select-Object -First 1
if (-not $versionMatch) {
    Write-Host "ERROR: Could not find PluginVersion in Plugin.cs." -ForegroundColor Red
    exit 1
}
$Version = $versionMatch.Matches.Groups[1].Value
Write-Host "Version: $Version" -ForegroundColor Gray
Write-Host ""

# Build
Write-Host "Building..." -ForegroundColor Yellow
Push-Location $ProjectDir
try {
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Build failed." -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

if (-not (Test-Path $DllPath)) {
    Write-Host "ERROR: Build output not found: $DllPath" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Prepare staging folder
$DistDir = Join-Path $ProjectDir "dist"
$StagingDir = Join-Path $DistDir "staging"
$ZipName = "AutoCrafterLimits-$Version.zip"
$ZipPath = Join-Path $DistDir $ZipName

if (Test-Path $StagingDir) {
    Remove-Item -Path $StagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

# BepInEx structure (extract-to-game-root friendly)
$PluginsDir = Join-Path $StagingDir "BepInEx\plugins"
New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null

# Copy DLL
Copy-Item -Path $DllPath -Destination $PluginsDir -Force
Write-Host "Packaging: BepInEx/plugins/AutoCrafterLimits.dll" -ForegroundColor Gray

# Create zip
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
if (Test-Path $ZipPath) {
    Remove-Item -Path $ZipPath -Force
}

Push-Location $StagingDir
try {
    Compress-Archive -Path * -DestinationPath $ZipPath -CompressionLevel Optimal
} finally {
    Pop-Location
}

# Cleanup staging
Remove-Item -Path $StagingDir -Recurse -Force

Write-Host ""
Write-Host "Pack complete." -ForegroundColor Green
Write-Host "Output: $ZipPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Install: Extract the zip into your game folder" -ForegroundColor Gray
Write-Host "  (e.g. C:\Program Files (x86)\Steam\steamapps\common\The Planet Crafter)" -ForegroundColor Gray
Write-Host "  BepInEx must be installed. The DLL goes to BepInEx/plugins/." -ForegroundColor Gray
