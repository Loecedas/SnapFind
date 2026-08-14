# build.ps1
# Automates the build and packaging process with dynamic versioning
param(
    [string]$Version,
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
    $versions = @()

    if (Test-Path $installersDir) {
        Get-ChildItem -Path $installersDir -Filter "SnapFindSetup_v*.exe" | ForEach-Object {
            if ($_.Name -match "SnapFindSetup_v(\d+)\.(\d+)\.(\d+)") {
                $v = [version]"$($Matches[1]).$($Matches[2]).$($Matches[3])"
                $versions += $v
            }
        }
    }

    if (Test-Path $portablesDir) {
        Get-ChildItem -Path $portablesDir -Filter "SnapFindPortable_v*.zip" | ForEach-Object {
            if ($_.Name -match "SnapFindPortable_v(\d+)\.(\d+)\.(\d+)") {
                $v = [version]"$($Matches[1]).$($Matches[2]).$($Matches[3])"
                $versions += $v
            }
        }
    }

    if ($versions.Count -gt 0) {
        $maxVersion = ($versions | Sort-Object -Descending)[0]
        $nextVersion = "$($maxVersion.Major).$($maxVersion.Minor).$($maxVersion.Build + 1)"
    } else {
        $nextVersion = "2.4.0"
    }
}

Write-Host "Determined next version: v$nextVersion" -ForegroundColor Green

# 3. Compile SnapFind (dotnet publish)
Write-Host "Compiling SnapFind..." -ForegroundColor Cyan
dotnet publish "$srcDir\SnapFind.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

$publishDir = "$srcDir\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

# Clean up unused default models folder copied by PaddleOCRSharp NuGet to root
$unusedInferenceDir = "$publishDir\inference"
if (Test-Path $unusedInferenceDir) {
    Write-Host "Cleaning up unused default models from publish root..." -ForegroundColor Yellow
    Remove-Item -Path $unusedInferenceDir -Recurse -Force
}

# 4. Copy SnapFind.exe to root folder
Write-Host "Copying executable to root..." -ForegroundColor Cyan
# Gracefully terminate running SnapFind instance to avoid file lock during copy
Get-Process -Name "SnapFind" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 200

$publishExe = "$publishDir\SnapFind.exe"
Copy-Item $publishExe "$workspaceRoot\SnapFind.exe" -Force

# Clean up variant executables from root
Get-ChildItem -Path "$workspaceRoot\SnapFind_*.exe" -ErrorAction Ignore | Remove-Item -Force

# 5. Generate portable ZIP (without timestamp)
if (-not $NoZip -and -not $ExeOnly) {
    Write-Host "Generating portable ZIP..." -ForegroundColor Cyan
    $zipTempDir = "$workspaceRoot\releases\portables\SnapFind"
    if (Test-Path $zipTempDir) { Remove-Item -Path $zipTempDir -Recurse -Force }
    New-Item -ItemType Directory -Path $zipTempDir | Out-Null

    # Copy published files into temp folder
    Copy-Item "$publishExe" "$zipTempDir\SnapFind.exe" -Force
    Copy-Item "$workspaceRoot\libs" "$zipTempDir\libs" -Recurse -Force

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
} else {
    if ($ExeOnly) {
        Write-Host "Skipping portable ZIP generation as requested (-ExeOnly is set)." -ForegroundColor Yellow
    } else {
        Write-Host "Skipping portable ZIP generation as requested (-NoZip is set)." -ForegroundColor Yellow
    }
}

# 6. Generate installer using Inno Setup (without timestamp)
if (-not $ExeOnly -and -not $NoInstaller) {
    Write-Host "Generating installer using Inno Setup..." -ForegroundColor Cyan
    $isccPath = "$workspaceRoot\cache\InnoSetup\ISCC.exe"
    if (Test-Path $isccPath) {
        & $isccPath /dAppVersion=$nextVersion "$srcDir\setup.iss"
        Write-Host "Installer generated successfully in: $installersDir" -ForegroundColor Green
    } else {
        Write-Warning "Inno Setup compiler (ISCC.exe) not found at: $isccPath. Skipping installer generation."
    }
} else {
    Write-Host "Skipping installer generation as requested (-ExeOnly is set)." -ForegroundColor Yellow
}

Write-Host "Build complete! Compiled and packaged version v$nextVersion successfully." -ForegroundColor Green
