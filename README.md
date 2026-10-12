<div align="center">

# SnapFind

<p>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8.0" /></a>
  <a href="./LICENSE.md"><img src="https://img.shields.io/badge/License-MIT-blue" alt="License" /></a>
  <a href="https://github.com/Loecedas/SnapFind/releases"><img src="https://img.shields.io/github/v/release/Loecedas/SnapFind?logo=github" alt="Latest Release" /></a>
  <a href="https://github.com/Loecedas/SnapFind/releases"><img src="https://img.shields.io/github/downloads/Loecedas/SnapFind/total?logo=github" alt="Downloads" /></a>
  <a href="https://github.com/Loecedas/SnapFind"><img src="https://img.shields.io/github/stars/Loecedas/SnapFind?logo=github" alt="Stars" /></a>
</p>

*一款专为 Windows 系统深度定制的高性能、纯本地离线截图 OCR 识别与搜索工具*

<p>
  <b>简体中文</b> | <a href="README.en.md">English</a>
</p>

</div>

---

**SnapFind** 是一款专为 Windows 系统深度定制的高性能、纯本地离线截图 OCR 识别与搜索工具。它集成轻量化 PaddleOCR 引擎，拥有毫秒级的截图唤起速度，所有数据 100% 本地处理，零网络请求、零隐私泄露风险，并提供极速复制、一键搜索与多图暂存抽屉等强大交互。

### 📊 为什么选择 SnapFind？

| 特性 / 指标 | 常见在线/云端 OCR | Electron 架构工具 | **SnapFind** |
| :--- | :---: | :---: | :---: |
| **数据隐私** | ⚠️ 截图需上传云端 | 视服务而定 | 🟢 **100% 纯本地离线，零泄露** |
| **后台待机内存** | ~50MB - 100MB | 🔴 **200MB - 500MB** | 🟢 **< 10MB（智能内存回收）** |
| **多界面连续暂存** | ❌ 不支持 | ❌ 多数不支持 | 🟢 **支持（多选区胶囊抽屉）** |
| **启动/响应速度** | 受网络延迟影响 | 较慢 | 🟢 **毫秒级极速唤起** |
| **Win11 原生契合** | 基础适配 | 网页渲染感 | 🟢 **Fluent 极细滚动条 / DWM 圆角** |

---

## 🎬 动态功能演示

### 1. 框选极速 OCR 与自动复制到剪贴板
按下全局热键（默认 `Ctrl + Alt + S`）唤起截图，框选文字区域后即可自动识别并写入系统剪贴板。

<p align="center">
  <img src="docs/images/1.gif" alt="框选后自动复制到剪切板" width="650" />
</p>

---

### 2. 同屏多选区连续框选
支持在单次截图会话中连续框选多个图文片段，左上方带有序号角标，多段内容自动按序汇总合并。

<p align="center">
  <img src="docs/images/2.gif" alt="同屏连续框选" width="650" />
</p>

---

### 3. 跨多应用/多界面连续截取与胶囊抽屉插入
支持在不同应用与窗口之间穿梭截图。点击【切换界面】后前序截取自动收纳至顶部暂存胶囊条，再次截图时画布 100% 纯净无历史重影；通过专属【插入】抽屉可随时预览高清缩略卡片、自由【上移】/【下移】调序、删除以及 `Ctrl + Z` 撤销恢复。

<p align="center">
  <img src="docs/images/3.gif" alt="连续在不同界面框选并进行插入" width="650" />
</p>

---

## ✨ 核心特性

- 🔒 **100% 本地离线推理**：集成 PaddleOCR v6 引擎，支持 `PP-OCRv6_tiny` 与 `PP-OCRv6_small` 双模型动态热切换，无需联网，隐私零外泄。
- ⚡ **智能内存自动回收**：识别完成后闲置 5 秒自动卸载推理引擎并调用 Windows `EmptyWorkingSet`，后台常驻内存从 ~40MB **骤降至 < 10MB**。
- 🧩 **模块化扩展插件架构**：主程序遵循极简原则零冗余打包，提供标准插件扩展接口。支持在控制中心一键导入、管理与热插拔插件，随需启用。
- 🌐 **多源智能翻译插件 (SnapFind.Plugin.Translation)**：
  - **同步翻译**：OCR 框选识别后自动同步翻译译文并在底部贴合展示（可设置开关，未勾选不触发）。
  - **独立翻译窗口**：支持全局热键随时唤起/关闭独立翻译浮窗，支持实时互译、简繁转换与智能网络代理。
  - **极简原生交互**：界面无冗余滚动条，一键 SVG 按钮复制译文，无缝契合系统主题。
- 🖥️ **多显示器与高 DPI 自适应**：精确适配多屏幕与不同 DPI 缩放比例，杜绝跨屏截图下的偏移和黑屏。
- 🎨 **Win11 原生现代 UI**：自适应系统暗色/亮色主题、Windows 11 原生 DWM 圆角与阴影特效、6px 极细 Fluent 动态滚动条。
- 🎛️ **四合一控制中心**：集成通用设置、扩展插件管理、关于与双源（GitHub / Gitee）并发自动更新通知，支持中英文双语一键无缝切换。
- ⌨️ **便捷快捷键体系**：
  - `Ctrl + Alt + S`：全局极速截图 OCR（支持在控制面板自定义）
  - `Ctrl + Alt + C`：一键呼出控制中心
  - `Ctrl + Alt + T`：唤起/关闭独立翻译窗口（需安装翻译插件，支持自定义）
  - `Ctrl + C`：结果框内快捷复制并自动关闭窗口
  - `Enter`：一键拉起系统默认浏览器进行网页检索
  - `Ctrl + Z`：暂存抽屉内快速撤销删除/调序操作

---

## 📂 项目结构

```text
SnapFind/
├── src/        # 核心源代码 (WPF / C#、UI 交互、热键钩子及单文件打包)
│   └── Plugins/ # 官方扩展插件源代码 (SnapFind.Plugin.Translation 等)
├── libs/       # PaddleOCR C++ 原生动态依赖项与离线推理模型
├── docs/       # 项目文档与演示图片资源 (docs/images/)
├── cache/      # 运行时用户偏好配置 (config.json)
├── releases/   # 安装包 (installers/)、便携包 (portables/) 与插件包 (plugins/)
├── tools/      # 便携版 7z SFX 自解压构建工具链
└── backup/     # 临时编译缓存与历史发布包一键清理脚本
```

---

## 🤖 OCR 引擎版本对比 (RapidOCR vs PaddleOCR)

SnapFind 提供两种 OCR 引擎架构以满足不同用户场景需求：

| 比较维度 | RapidOCR 极速版 (当前默认推荐) | PaddleOCR 原生版 |
| :--- | :--- | :--- |
| **底层推理引擎** | 微软 **ONNX Runtime** | 百度 **Paddle Inference** (C++ 原生) |
| **安装包体积** | 🟢 **~28 MB** (锐减 72%) | 🔴 **~100 MB** |
| **便携压缩包体积** | 🟢 **~32 MB** (锐减 77%) | 🔴 **~142 MB** |
| **模型库体积** | 🟢 **~16 MB** (PP-OCRv4 ONNX 精简模型) | 🔴 **~40 MB** + 250MB 原生依赖库 |
| **运行依赖与环境** | 🟢 **纯净零冲突**，无需外部复杂 C++ 动态库 | 🟡 依赖 20+ 个 C++ 原生 DLL 及特定 VC++ 运行时 |
| **冷启动与响应速度** | 🟢 **毫秒级极速响应**，无额外初始化开销 | 🟡 引擎加载耗时较长 |
| **内存占用与释放** | 🟢 识别后自动释放，待机内存低 | 🟢 识别后自动调用底层 MKL 内存释放 |
| **复杂/倾斜大图识别** | 🟢 针对屏幕截图、代码及 UI 文本识别极佳 | 🟢 在倾斜极大或超大密集文档上具备更深度的调优 |

### 💡 优缺点分析

#### 🚀 RapidOCR 版本
- **优点 (Pros)**：
  1. **极致轻巧**：安装包仅 28MB，相比原版减少 70% 以上体积，便携版仅 32MB。
  2. **启动迅速、兼容性高**：基于微软现代化 ONNX Runtime，无老旧 VC++ 运行库冲突，系统冷启动极快。
  3. **开箱即用**：内置精炼优化的 PP-OCRv4 中英文检测与识别模型，满足日常截图与办公的全部需求。
- **缺点 (Cons)**：
  1. 专注于标准化轻量 ONNX 模型，若需使用非常规特殊定制算子扩展性略受限。

#### 🛡️ PaddleOCR 版本
- **优点 (Pros)**：
  1. **官方原生算法**：直接对接 PaddlePaddle 官方原生算子库，原生支持 PP-OCRv6 算法架构与服务器端大模型。
  2. **密集复杂排版调优**：在超大分辨率倾斜扫描件或极特殊密集排版上具备更深度的算子支持。
- **缺点 (Cons)**：
  1. **体积庞大**：需要携带近 300MB 的 Paddle C++ 原生动态链接库（MKL、OpenCV、mkldnn 等）。
  2. **环境敏感**：对底层系统的 VC++ 运行库版本要求更严格，容易发生动态库版本加载冲突。

---

## 🚀 快速上手

### 环境要求
- 操作系统：Windows 10 / Windows 11 (64 位)
- 运行环境：[.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### 安装与使用
1. **下载使用**：前往 [Releases 页面](https://github.com/Loecedas/SnapFind/releases) 下载最新版的安装包（`SnapFindSetup_*.exe`）或免安装自解压便携版（`SnapFindPortable_*.7z.exe`）。
2. **源码调试**：
   ```bash
   git clone https://github.com/Loecedas/SnapFind.git
   cd SnapFind/src
   # 编译运行 RapidOCR 版本（推荐）
   dotnet run --project SnapFind.Rapid.csproj
   # 或编译运行 PaddleOCR 原生版本
   dotnet run --project SnapFind.csproj
   ```

---

## 🧩 扩展插件安装与使用

SnapFind 采用纯净的外部插件架构，核心包零捆绑无冗余。您可以根据需要下载安装官方扩展插件：

### 安装官方翻译插件 (`SnapFind.Plugin.Translation`)
1. **获取插件包**：前往 [Releases 页面](https://github.com/Loecedas/SnapFind/releases) 下载 `SnapFind.Plugin.Translation_v*.zip`。
2. **导入安装**：
   - **方式一（推荐）**：按下 `Ctrl + Alt + C` 打开控制中心，切换至【扩展插件】标签页，点击【导入插件包】选择下载的 zip 文件即可自动解压安装并即时热加载生效。
   - **方式二（手动）**：将 zip 包内容解压至 SnapFind 运行目录下的 `plugins/SnapFind.Plugin.Translation` 文件夹中。
3. **功能配置**：
   - 安装插件后，控制中心的【设置】页面将动态呈现【智能翻译插件】配置区块。
   - 可随需勾选【OCR 识别后同步翻译】；未勾选时仅作文本提取，不会发起任何翻译操作。
   - 支持自定义独立翻译浮窗快捷键（默认 `Ctrl + Alt + T`），随时呼出进行多语言文本互译与简繁转换。

---

## ❓ 常见问题

**Q: 运行绿色版时弹出 `DllNotFoundException` 错误？**
> **A**: 请确保 `libs/` 依赖目录与主程序 `SnapFind.exe` 处于同级目录下。该目录包含 OCR 所需的原生动态链接库与离线模型。

**Q: 全局快捷键按下没有反应？**
> **A**: 该快捷键可能被系统中其他软件占用。可右键托盘图标或按 `Ctrl + Alt + C` 打开控制中心重新绑定快捷键。

**Q: 开机自启动未生效？**
> **A**: 软件通过当前用户注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 实现自启，请确认杀毒软件未拦截注册表写入。

---

## 📄 许可证与致谢

- 本项目基于 [MIT License](LICENSE.md)（[中文版](LICENSE.zh.md)）许可协议开源。
- 感谢 [RapidOCR](https://github.com/RapidAI/RapidOCR)、[PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) 与 [Inno Setup](https://jrsoftware.org/isinfo.php) 提供的卓越技术支持。

> **免责声明**：本项目所有 OCR 推理均在本地计算完成，绝不会将您的屏幕画面或文本数据上传至任何第三方服务器。
