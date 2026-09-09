# build.ps1
# 本地打包脚本：生成 Rapid / Paddle 双版本的便携包与安装包
param(
    [string]$Version = "2.4.5",
    [ValidateSet("", "Rapid", "Paddle")]
    [string]$Edition = "",
    [switch]$NoZip,
    [switch]$ExeOnly,
    [switch]$NoInstaller
)

$ErrorActionPreference = "Stop"

# 1. 解析目录
$workspaceRoot = Resolve-Path "$PSScriptRoot\.."
$srcDir = $PSScriptRoot
$installersDir = "$workspaceRoot\releases\installers"
$portablesDir = "$workspaceRoot\releases\portables"

if (-not (Test-Path $installersDir))  { New-Item -ItemType Directory -Path $installersDir  -Force | Out-Null }
if (-not (Test-Path $portablesDir)) { New-Item -ItemType Directory -Path $portablesDir -Force | Out-Null }

# 2. 版本号
$nextVersion = $Version.TrimStart('v')
if (-not $nextVersion) { $nextVersion = "2.4.5" }

# 3. 确定要打包的版本
$editions = if ($Edition) { @($Edition) } else { @("Rapid", "Paddle") }

Write-Host "Packaging version: v$nextVersion (Editions: $($editions -join ', '))" -ForegroundColor Green

# 4. 终止运行中的实例，避免文件锁
Get-Process -Name "SnapFind", "SnapFind_Rapid" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

# 5. 逐版本编译 + 打包
foreach ($ed in $editions) {
    $project = if ($ed -eq "Rapid") { "SnapFind.Rapid.csproj" } else { "SnapFind.csproj" }
    $publishDir = "$srcDir\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

    Write-Host "Compiling SnapFind ($ed)..." -ForegroundColor Cyan
    dotnet publish "$srcDir\$project" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:Version=$nextVersion -o $publishDir

    $publishExe = "$publishDir\SnapFind.exe"

    # 6. 复制到根目录（绿色版）
    if (-not $NoZip) {
        Write-Host "Copying executable to root..." -ForegroundColor Cyan
        Copy-Item $publishExe "$workspaceRoot\SnapFind.exe" -Force
        Copy-Item $publishExe "$workspaceRoot\SnapFind_$ed.exe" -Force
    }

    # 7. 便携 ZIP
    if (-not $NoZip -and -not $ExeOnly) {
        Write-Host "Generating portable ZIP ($ed)..." -ForegroundColor Cyan
        $zipTempDir = "$portablesDir\SnapFind"
        if (Test-Path $zipTempDir) { Remove-Item -Path $zipTempDir -Recurse -Force }
        New-Item -ItemType Directory -Path "$zipTempDir" -Force | Out-Null
        Copy-Item $publishExe "$zipTempDir\SnapFind.exe" -Force
        if ($ed -eq "Rapid") {
            New-Item -ItemType Directory -Path "$zipTempDir\libs\rapid" -Force | Out-Null
            Copy-Item "$workspaceRoot\libs\rapid\*" "$zipTempDir\libs\rapid" -Recurse -Force
        } else {
            New-Item -ItemType Directory -Path "$zipTempDir\libs" -Force | Out-Null
            Copy-Item "$workspaceRoot\libs\*" "$zipTempDir\libs" -Recurse -Force
        }

        $zipDest = "$portablesDir\SnapFindPortable_$ed`_v$nextVersion.zip"
        if (Test-Path $zipDest) { Remove-Item $zipDest -Force }

        $exe7z = "C:\Program Files\7-Zip\7z.exe"
        if (Test-Path $exe7z) {
            & $exe7z a -tzip -m0=Deflate -mx=5 -mmt=on "$zipDest" "$zipTempDir"
        } else {
            Compress-Archive -Path "$zipTempDir" -DestinationPath "$zipDest" -CompressionLevel Optimal
        }

        Remove-Item -Path $zipTempDir -Recurse -Force
        Write-Host "Portable ZIP generated at: $zipDest" -ForegroundColor Green
    }

    # 8. 安装包（Inno Setup）
    if (-not $ExeOnly -and -not $NoInstaller) {
        Write-Host "Generating installer ($ed)..." -ForegroundColor Cyan
        $isccPath = "$workspaceRoot\tools\InnoSetup\ISCC.exe"
        if (Test-Path $isccPath) {
            & $isccPath /DAppVersion=$nextVersion /DEdition=$ed /DPublishDir="$publishDir" /O"$installersDir" "$srcDir\setup.iss"
            Write-Host "Installer generated successfully in: $installersDir" -ForegroundColor Green
        } else {
            Write-Warning "Inno Setup compiler (ISCC.exe) not found at: $isccPath. Skipping installer generation."
        }
    }
}

Write-Host "Build complete! Compiled and packaged version v$nextVersion successfully." -ForegroundColor Green
