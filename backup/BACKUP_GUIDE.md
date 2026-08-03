# SnapFind 备份前清理指南 (Backup Cleanup Guide)

本指南旨在指导如何在备份项目前进行文件清理，以最大程度减小备份包的体积。此目录（`backup/`）为备份专用工具目录，固定存放本指南及自动化清理脚本，请勿在此存放其他无关文件。

---

## 🧹 推荐删除的文件与目录 (Safe to Delete)

在备份前，以下目录和文件可以**安全删除**，它们是编译临时文件或生成包，删除后不会丢失任何开发历史或核心依赖，且可随时重新生成：

| 目录/文件路径 | 说明 | 为什么可以删除 |
| :--- | :--- | :--- |
| `src/bin/` | C# 项目编译输出目录 | 重新编译（Debug/Release）时会自动生成。 |
| `src/obj/` | C# 编译中间文件目录 | 重新编译时会自动生成。 |
| `releases/installers/*` | 生成的安装包 `.exe` 文件 | 通过 `src/build.ps1` 脚本重新打包时会重新生成。 |
| `releases/portables/*` | 生成的免安装 `.zip` 文件 | 重新打包时会重新生成。 |
| `SnapFind.exe` (根目录) | 绿色版主可执行文件 | 由 `src` 编译生成，可随时重新生成并拷贝到根目录。 |

---

## 💾 必须保留的文件与目录 (Must Keep)

为了保证项目可以正常编译、运行、并保留完整的开发历史，以下目录**必须保留**，千万不要删除：

| 目录/文件路径 | 说明 | 为什么必须保留 |
| :--- | :--- | :--- |
| `src/` (除 bin/obj 外) | 软件源代码目录 | 核心代码、XAML 页面及项目文件（`SnapFind.csproj`）。 |
| `libs/` | PaddleOCR 及 OpenCV 原生依赖库 | 离线文字识别所必需的本地 C++ 动态链接库（DLL）和识别模型。 |
| `cache/` | 配置与编译器缓存 | 包含运行时配置，以及 `cache/InnoSetup` 编译器（打包安装程序所必需）。 |
| `backup/` | 备份专用工具目录 | 固定存放 `BACKUP_GUIDE.md` 和 `backup.ps1`。请勿存放其他文件。 |
| `.git/` | Git 版本控制数据库 | **绝对不要手动删除 `.git/objects/` 下的任何文件**，否则会导致 Git 仓库损坏（如 `bad object HEAD`）。 |
| `.agents/` | Agent 规范与规则配置 | 存放 AI 协作的规范文件。 |

---

## ⚡ 自动化一键清理脚本 (Automated Cleanup Script)

我们为您在当前目录下准备了自动化清理脚本 `backup.ps1`。您只需右键通过 PowerShell 运行它，它会自动寻找项目根目录，清理所有上述可安全删除的临时及生成文件，并在完成后通知您。您随后可以直接将整个目录打包备份。

脚本内容如下：

```powershell
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
```
