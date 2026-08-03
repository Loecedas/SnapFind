# build.ps1
# Automates the build and packaging process with dynamic versioning

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

$nextVersion = "1.0.0"
if ($versions.Count -gt 0) {
    $maxVersion = ($versions | Sort-Object -Descending | Select-Object -First 1)
    $major = $maxVersion.Major
    $minor = $maxVersion.Minor
    $patch = $maxVersion.Build
    
    if ($patch -lt 9) {
        $patch += 1
    } else {
        if ($minor -lt 9) {
            $minor += 1
            $patch = 0
        } else {
            $major += 1
            $minor = 0
            $patch = 0
        }
    }
    $nextVersion = "$major.$minor.$patch"
}

Write-Host "Determined next version: v$nextVersion" -ForegroundColor Green

# 3. Compile SnapFind (dotnet publish)
Write-Host "Compiling SnapFind..." -ForegroundColor Cyan
dotnet publish "$srcDir\SnapFind.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# 4. Copy SnapFind.exe to root folder
Write-Host "Copying executable to root..." -ForegroundColor Cyan
$publishExe = "$srcDir\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\SnapFind.exe"
$rootExe = "$workspaceRoot\SnapFind.exe"
Copy-Item $publishExe $rootExe -Force

# 5. Generate portable ZIP
$zipName = "SnapFindPortable_v$($nextVersion).zip"
Write-Host "Creating portable ZIP: $zipName..." -ForegroundColor Cyan
$publishDir = "$srcDir\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

# Guarantee libs folder containing native DLLs is fully copied to publish output
$publishLibsDir = "$publishDir\libs"
if (-not (Test-Path $publishLibsDir)) { New-Item -ItemType Directory -Path $publishLibsDir -Force | Out-Null }
Write-Host "Syncing libs folder to publish output..." -ForegroundColor Yellow
Copy-Item "$workspaceRoot\libs\*" $publishLibsDir -Recurse -Force

# Clean up unused default models folder copied by PaddleOCRSharp NuGet to root
$unusedInferenceDir = "$publishDir\inference"
if (Test-Path $unusedInferenceDir) {
    Write-Host "Cleaning up unused default models from publish root..." -ForegroundColor Yellow
    Remove-Item -Path $unusedInferenceDir -Recurse -Force
}

$zipDest = "$portablesDir\$zipName"

Add-Type -Assembly "System.IO.Compression.FileSystem"
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipDest)

# 6. Compile installer using Inno Setup
Write-Host "Compiling installer..." -ForegroundColor Cyan
$isccPath = "$workspaceRoot\cache\InnoSetup\ISCC.exe"
if (-not (Test-Path $isccPath)) {
    # If not in cache, try default Program Files paths just in case
    if (Test-Path "C:\Program Files (x86)\Inno Setup 6\ISCC.exe") {
        $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    } elseif (Test-Path "C:\Program Files\Inno Setup 6\ISCC.exe") {
        $isccPath = "C:\Program Files\Inno Setup 6\ISCC.exe"
    } else {
        throw "ISCC.exe not found! Please make sure Inno Setup is installed."
    }
}

Start-Process -FilePath $isccPath -ArgumentList "/DAppVersion=$nextVersion `"$srcDir\setup.iss`"" -NoNewWindow -Wait

Write-Host "Build and packaging complete! Generated version v$nextVersion" -ForegroundColor Green
