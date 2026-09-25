# SnapFind 一键清理脚本

# 1. 获取脚本所在目录及项目根目录
$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$projectRoot = (Get-Item $scriptDir).Parent.FullName

Write-Host "开始清理临时编译文件、发布包及根目录可执行程序..." -ForegroundColor Cyan

# 2. 清理 C# 编译缓存与临时目录
$cleanupDirs = @(
    (Join-Path $projectRoot "src\bin"),
    (Join-Path $projectRoot "src\obj"),
    (Join-Path $projectRoot "tests\bin"),
    (Join-Path $projectRoot "tests\obj"),
    (Join-Path $projectRoot "releases\tmp")
)
foreach ($dir in $cleanupDirs) {
    if (Test-Path $dir) {
        Remove-Item -Path $dir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "已清理 $dir" -ForegroundColor Yellow
    }
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

# 4. 清理根目录下的免安装主可执行文件 SnapFind.exe 与 SnapFind_*.exe
Get-ChildItem -Path $projectRoot -Filter "SnapFind*.exe" -File -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
    Write-Host "已清理根目录下的可执行程序 $($_.Name)" -ForegroundColor Yellow
}

Write-Host "清理完成！所有可删除的临时文件、打包文件及可执行程序均已删除干净。" -ForegroundColor Green
