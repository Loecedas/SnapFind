# build.ps1
# Automates the build and packaging process with dynamic versioning for Rapid release
param(
    [string]$Version = "2.4.5",
    [switch]$NoZip,
    [switch]$ExeOnly,
    [switch]$NoInstaller
)

$ErrorActionPreference = "Stop"

# 1. Determine directories
$workspaceRoot = Resolve-Path "$PSScriptRoot\.."
$srcDir = "$PSScriptRoot"
$installersDir = "$workspaceRoot\releases\installers"
$portablesDir = "$workspaceRoot\releases\portables"

# Ensure output directories exist
if (-not (Test-Path $installersDir)) { New-Item -ItemType Directory -Path $installersDir -Force | Out-Null }
if (-not (Test-Path $portablesDir)) { New-Item -ItemType Directory -Path $portablesDir -Force | Out-Null }

# 2. Get next version number
if ($Version) {
    $nextVersion = $Version.TrimStart('v')
} else {
    $nextVersion = "2.4.5"
}

Write-Host "Packaging version: v$nextVersion (RapidOCR release)" -ForegroundColor Green

# 3. Gracefully terminate running instances to avoid file locks
Get-Process -Name "SnapFind", "SnapFind_Rapid" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

# 4. Compile SnapFind (RapidOCR release)
Write-Host "Compiling SnapFind (RapidOCR)..." -ForegroundColor Cyan
dotnet publish "$srcDir\SnapFind.Rapid.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

$publishDir = "$srcDir\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
$publishExe = "$publishDir\SnapFind.exe"

# 5. Copy executable to root folder
Write-Host "Copying executable to root..." -ForegroundColor Cyan
Copy-Item $publishExe "$workspaceRoot\SnapFind.exe" -Force
Copy-Item $publishExe "$workspaceRoot\SnapFind_Rapid.exe" -Force

# 6. Generate portable ZIP (contains SnapFind.exe and libs/rapid)
if (-not $NoZip -and -not $ExeOnly) {
    Write-Host "Generating portable ZIP (Rapid)..." -ForegroundColor Cyan
    $zipTempDir = "$workspaceRoot\releases\portables\SnapFind"
    if (Test-Path $zipTempDir) { Remove-Item -Path $zipTempDir -Recurse -Force }
    New-Item -ItemType Directory -Path "$zipTempDir\libs" -Force | Out-Null

    # Copy published files into temp folder
    Copy-Item "$publishExe" "$zipTempDir\SnapFind.exe" -Force
    Copy-Item "$workspaceRoot\libs\rapid" "$zipTempDir\libs\rapid" -Recurse -Force

    $zipDest = "$portablesDir\SnapFindPortable_v$nextVersion.zip"
    if (Test-Path $zipDest) { Remove-Item -Path $zipDest -Force }

    # Use 7-Zip Deflate Ultra if available (ZIP format, .NET ZipFile compatible), fallback to Compress-Archive
    $exe7z = "C:\Program Files\AMD\CIM\Bin64\7z.exe"
    if (Test-Path $exe7z) {
        Write-Host "Compressing portable ZIP using 7-Zip (Deflate mx=5)..." -ForegroundColor Cyan
        & $exe7z a -tzip -m0=Deflate -mx=5 -mmt=on "$zipDest" "$zipTempDir"
    } else {
        Write-Host "7-Zip not found, falling back to Compress-Archive (.zip)..." -ForegroundColor Yellow
        Compress-Archive -Path "$zipTempDir" -DestinationPath "$zipDest" -CompressionLevel Optimal
    }

    # Clean up temp folder
    Remove-Item -Path $zipTempDir -Recurse -Force
    Write-Host "Portable ZIP generated at: $zipDest" -ForegroundColor Green
}

# 7. Generate installer using Inno Setup
if (-not $ExeOnly -and -not $NoInstaller) {
    Write-Host "Generating installer using Inno Setup (Rapid)..." -ForegroundColor Cyan
    $isccPath = "$workspaceRoot\cache\InnoSetup\ISCC.exe"
    if (Test-Path $isccPath) {
        & $isccPath /dAppVersion=$nextVersion "$srcDir\setup.iss"
        Write-Host "Installer generated successfully in: $installersDir" -ForegroundColor Green
    } else {
        Write-Warning "Inno Setup compiler (ISCC.exe) not found at: $isccPath. Skipping installer generation."
    }
}

Write-Host "Build complete! Compiled and packaged version v$nextVersion successfully." -ForegroundColor Green
