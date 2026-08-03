# SnapFind 一键清理脚本

# 1. 获取脚本所在目录及项目根目录
$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$projectRoot = (Get-Item $scriptDir).Parent.FullName

Write-Host "开始清理临时编译文件、发布包及根目录可执行程序..." -ForegroundColor Cyan

# 2. 清理 C# 编译缓存
$binPath = Join-Path $projectRoot "src\bin"
$objPath = Join-Path $projectRoot "src\obj"
if (Test-Path $binPath) { 
    Remove-Item -Path $binPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "已清理 src\bin" -ForegroundColor Yellow
}
if (Test-Path $objPath) { 
    Remove-Item -Path $objPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "已清理 src\obj" -ForegroundColor Yellow
}

# 3. 清理生成的发布包
$installersPath = Join-Path $projectRoot "releases\installers\*"
$portablesPath = Join-Path $projectRoot "releases\portables\*"
if (Test-Path (Join-Path $projectRoot "releases\installers")) { 
    Remove-Item -Path $installersPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "已清理 releases\installers\ 下的历史安装程序" -ForegroundColor Yellow
}
if (Test-Path (Join-Path $projectRoot "releases\portables")) { 
    Remove-Item -Path $portablesPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "已清理 releases\portables\ 下的历史免安装包" -ForegroundColor Yellow
}

# 4. 清理根目录下的免安装主可执行文件 SnapFind.exe
$exePath = Join-Path $projectRoot "SnapFind.exe"
if (Test-Path $exePath) {
    Remove-Item -Path $exePath -Force -ErrorAction SilentlyContinue
    Write-Host "已清理根目录下的主可执行程序 SnapFind.exe" -ForegroundColor Yellow
}

Write-Host "清理完成！所有可删除的临时文件、打包文件及可执行程序均已删除干净。" -ForegroundColor Green
