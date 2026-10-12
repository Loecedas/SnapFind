param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$projectFile = "$scriptDir\SnapFind.Plugin.Translation.csproj"
$outDir = "$scriptDir\dist"
$workspaceRoot = Resolve-Path "$scriptDir\..\..\.."
$releasesPluginDir = "$workspaceRoot\releases\plugins"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Building SnapFind.Plugin.Translation v$Version..." -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Clean output folder
if (Test-Path $outDir) {
    Remove-Item -Path $outDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

# 2. Build Release DLL
dotnet build $projectFile -c $Configuration -o "$outDir"

Write-Host "`nPlugin DLL compiled at: $outDir\SnapFind.Plugin.Translation.dll" -ForegroundColor Green

# 3. Package ZIP into releases/plugins directory
if (-not (Test-Path $releasesPluginDir)) {
    New-Item -ItemType Directory -Path $releasesPluginDir -Force | Out-Null
}

$tempPackDir = "$env:TEMP\SnapFind_Plugin_Pack_Temp"
if (Test-Path $tempPackDir) { Remove-Item -Path $tempPackDir -Recurse -Force }
New-Item -ItemType Directory -Path "$tempPackDir\SnapFind.Plugin.Translation\bin" -Force | Out-Null

# Copy binaries
Copy-Item "$outDir\*" "$tempPackDir\SnapFind.Plugin.Translation\bin\" -Force

# Copy documents and metadata to root of zip
if (Test-Path "$scriptDir\插件接入与适配指南.md") {
    Copy-Item "$scriptDir\插件接入与适配指南.md" "$tempPackDir\SnapFind.Plugin.Translation\" -Force
}
if (Test-Path "$scriptDir\README.md") {
    Copy-Item "$scriptDir\README.md" "$tempPackDir\SnapFind.Plugin.Translation\" -Force
}
if (Test-Path "$scriptDir\plugin.json") {
    Copy-Item "$scriptDir\plugin.json" "$tempPackDir\SnapFind.Plugin.Translation\" -Force
}

$zipFileName = "SnapFind.Plugin.Translation_v$Version.zip"
$zipTarget = "$releasesPluginDir\$zipFileName"

if (Test-Path $zipTarget) { Remove-Item -Path $zipTarget -Force }

Compress-Archive -Path "$tempPackDir\SnapFind.Plugin.Translation" -DestinationPath "$zipTarget" -CompressionLevel Optimal

Remove-Item -Path $tempPackDir -Recurse -Force

Write-Host "Plugin package ZIP successfully created at: $zipTarget" -ForegroundColor Green
