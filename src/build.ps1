# build.ps1
# 本地打包脚本：生成 Rapid / Paddle 双版本的便携包与安装包
param(
    [string]$Version = "2.5.2",
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
if (-not $nextVersion) { $nextVersion = "2.5.2" }

# 3. 确定要打包的版本
$editions = if ($Edition) { @($Edition) } else { @("Rapid", "Paddle") }

Write-Host "Packaging version: v$nextVersion (Editions: $($editions -join ', '))" -ForegroundColor Green

# 4. 终止运行中的实例，避免文件锁
Get-Process -Name "SnapFind", "SnapFind_Rapid", "SnapFind_Paddle" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 5. 逐版本编译 + 打包
foreach ($ed in $editions) {
    $project = if ($ed -eq "Rapid") { "SnapFind.Rapid.csproj" } else { "SnapFind.csproj" }
    $publishDir = "$srcDir\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

    Write-Host "Compiling SnapFind ($ed)..." -ForegroundColor Cyan
    dotnet publish "$srcDir\$project" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:Version=$nextVersion -o $publishDir

    $publishExe = "$publishDir\SnapFind.exe"

    # 6. 复制到根目录（绿色版：仅保留 SnapFind_Rapid.exe 与 SnapFind_Paddle.exe）
    if (-not $NoZip) {
        Write-Host "Copying executable to root..." -ForegroundColor Cyan
        Copy-Item $publishExe "$workspaceRoot\SnapFind_$ed.exe" -Force
        if (Test-Path "$workspaceRoot\SnapFind.exe") {
            Remove-Item "$workspaceRoot\SnapFind.exe" -Force -ErrorAction SilentlyContinue
        }
    }

    # 7. 便携包（标准 .zip 格式）
    if (-not $NoZip -and -not $ExeOnly) {
        Write-Host "Generating portable ZIP package ($ed)..." -ForegroundColor Cyan
        $zipTempDir = "$portablesDir\SnapFind"
        if (Test-Path $zipTempDir) { Remove-Item -Path $zipTempDir -Recurse -Force }
        New-Item -ItemType Directory -Path "$zipTempDir" -Force | Out-Null
        Copy-Item $publishExe "$zipTempDir\SnapFind.exe" -Force
        if ($ed -eq "Rapid") {
            New-Item -ItemType Directory -Path "$zipTempDir\libs\rapid" -Force | Out-Null
            Copy-Item "$workspaceRoot\libs\rapid\*" "$zipTempDir\libs\rapid" -Recurse -Force
        } else {
            New-Item -ItemType Directory -Path "$zipTempDir\libs" -Force | Out-Null
            Get-ChildItem "$workspaceRoot\libs" | Where-Object { $_.Name -ne "rapid" } | Copy-Item -Destination "$zipTempDir\libs" -Recurse -Force
        }

        # 优先使用 7-Zip 工具快速高压缩比生成 .zip，其次使用 Compress-Archive
        $local7z = "$workspaceRoot\tools\7-Zip\7z.exe"
        $exe7z = if (Test-Path $local7z) { $local7z } elseif (Get-Command 7z -ErrorAction SilentlyContinue) { (Get-Command 7z).Source } elseif (Test-Path "C:\Program Files\7-Zip\7z.exe") { "C:\Program Files\7-Zip\7z.exe" } else { $null }

        $zipDest = "$portablesDir\SnapFindPortable_$ed`_v$nextVersion.zip"
        if (Test-Path $zipDest) { Remove-Item $zipDest -Force }

        if ($exe7z) {
            Write-Host "Creating portable .zip package using $exe7z..." -ForegroundColor Cyan
            Push-Location $zipTempDir
            try {
                & $exe7z a -tzip -mx=9 -mmt=on "$zipDest" *
            } finally {
                Pop-Location
            }
            Write-Host "Portable ZIP generated at: $zipDest" -ForegroundColor Green
        } else {
            Compress-Archive -Path "$zipTempDir\*" -DestinationPath "$zipDest" -CompressionLevel Optimal
            Write-Host "Portable ZIP generated at: $zipDest" -ForegroundColor Yellow
        }

        Remove-Item -Path $zipTempDir -Recurse -Force
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

# 9. 扩展插件发布包（专门存放在 releases/plugins/）
if (-not $ExeOnly) {
    $pluginScript = "$workspaceRoot\src\Plugins\SnapFind.Plugin.Translation\build-plugin.ps1"
    if (Test-Path $pluginScript) {
        Write-Host "Packaging plugin releases to releases/plugins..." -ForegroundColor Cyan
        & pwsh -File "$pluginScript" -Version "1.0.0"
    }
}

Write-Host "Build complete! Compiled and packaged version v$nextVersion successfully." -ForegroundColor Green
