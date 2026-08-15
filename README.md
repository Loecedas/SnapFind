<div align="center">

# SnapFind

<p>
  <a href="https://github.com/Loecedas/SnapFind"><img src="https://img.shields.io/badge/Platform-Win%2010%20%7C%2011%20(x64)-0078D4?logo=windows&logoColor=white" alt="Platform" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8.0" /></a>
  <a href="https://github.com/PaddlePaddle/PaddleOCR"><img src="https://img.shields.io/badge/OCR-PaddleOCR%20v6-FF4500?logo=baidu&logoColor=white" alt="PaddleOCR" /></a>
  <a href="https://github.com/Loecedas/SnapFind"><img src="https://img.shields.io/badge/Standby%20RAM-%3C%2010%20MB-brightgreen?logo=ram&logoColor=white" alt="RAM" /></a>
  <a href="./LICENSE.md"><img src="https://img.shields.io/badge/License-MIT-blue" alt="License" /></a>
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
- 🖥️ **多显示器与高 DPI 自适应**：精确适配多屏幕与不同 DPI 缩放比例，杜绝跨屏截图下的偏移和黑屏。
- 🎨 **Win11 原生现代 UI**：自适应系统暗色/亮色主题、Windows 11 原生 DWM 圆角与阴影特效、6px 极细 Fluent 动态滚动条。
- 🎛️ **三合一控制中心**：集成设置、关于与双源（GitHub / Gitee）并发自动更新通知，支持中英文双语一键无缝切换。
- ⌨️ **便捷快捷键体系**：
  - `Ctrl + Alt + S`：全局极速截图 OCR（支持在控制面板自定义）
  - `Ctrl + Alt + C`：一键呼出控制中心
  - `Ctrl + C`：结果框内快捷复制并自动关闭窗口
  - `Enter`：一键拉起系统默认浏览器进行网页检索
  - `Ctrl + Z`：暂存抽屉内快速撤销删除/调序操作

---

## 📂 项目结构

```text
SnapFind/
├── src/        # 核心源代码 (WPF / C#、UI 交互、热键钩子及单文件打包)
├── libs/       # PaddleOCR C++ 原生动态依赖项与离线推理模型
├── docs/       # 项目文档与演示图片资源 (docs/images/)
├── cache/      # 运行时用户偏好配置 (config.json)
├── releases/   # 自动化生成的安装包 (installers/) 与免安装绿色版 (portables/)
└── backup/     # 临时编译缓存与历史发布包一键清理脚本
```

---

## 🚀 快速上手

### 环境要求
- 操作系统：Windows 10 / Windows 11 (64 位)
- 运行环境：[.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### 安装与使用
1. **下载使用**：前往 [Releases 页面](https://github.com/Loecedas/SnapFind/releases) 下载最新版的安装包（`SnapFindSetup_vX.Y.Z.exe`）或免安装便携版（`SnapFindPortable_vX.Y.Z.zip`）。
2. **源码调试**：
   ```bash
   git clone https://github.com/Loecedas/SnapFind.git
   cd SnapFind/src
   dotnet run
   ```

---

## ❓ 常见问题

**Q: 运行绿色版时弹出 `DllNotFoundException` 错误？**
> **A**: 请确保 `libs/` 依赖目录与主程序 `SnapFind.exe` 处于同级目录下。该目录包含 PaddleOCR 所需的原生 C++ 动态链接库和离线模型。

**Q: 全局快捷键按下没有反应？**
> **A**: 该快捷键可能被系统中其他软件占用。可右键托盘图标或按 `Ctrl + Alt + C` 打开控制中心重新绑定快捷键。

**Q: 开机自启动未生效？**
> **A**: 软件通过当前用户注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 实现自启，请确认杀毒软件未拦截注册表写入。

---

## 📄 许可证与致谢

- 本项目基于 [MIT License](LICENSE.md)（[中文版](LICENSE.zh.md)）许可协议开源。
- 感谢 [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) 与 [Inno Setup](https://jrsoftware.org/isinfo.php) 提供的卓越技术支持。

> **免责声明**：本项目所有 OCR 推理均在本地计算完成，绝不会将您的屏幕画面或文本数据上传至任何第三方服务器。
