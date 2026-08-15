<div align="center">

# SnapFind

<p>
  <a href="https://github.com/Loecedas/SnapFind"><img src="https://img.shields.io/badge/Platform-Win%2010%20%7C%2011%20(x64)-0078D4?logo=windows&logoColor=white" alt="Platform" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8.0" /></a>
  <a href="https://github.com/PaddlePaddle/PaddleOCR"><img src="https://img.shields.io/badge/OCR-PaddleOCR%20v6-FF4500?logo=baidu&logoColor=white" alt="PaddleOCR" /></a>
  <a href="https://github.com/Loecedas/SnapFind"><img src="https://img.shields.io/badge/Standby%20RAM-%3C%2010%20MB-brightgreen?logo=ram&logoColor=white" alt="RAM" /></a>
  <a href="./LICENSE.md"><img src="https://img.shields.io/badge/License-MIT-blue" alt="License" /></a>
</p>

*High-performance, offline-first local screenshot OCR and instant search tool deeply customized for Windows*

<p>
  <a href="README.md">简体中文</a> | <b>English</b>
</p>

</div>

---

**SnapFind** is a high-performance, lightweight, and offline-first screenshot OCR and search utility deeply customized for Windows. Integrating the PaddleOCR engine, it offers millisecond-level responsiveness. All screen-capturing and text extraction actions run 100% locally on your machine, protecting user privacy with zero data transmission, whilst providing quick search, automated copy-to-close workflows, and multi-session staged drawers.

### 📊 Why Choose SnapFind?

| Features / Metrics | Common Online/Cloud OCR | Electron-based Tools | **SnapFind** |
| :--- | :---: | :---: | :---: |
| **Data Privacy** | ⚠️ Uploads screenshots to cloud | Depends on service | 🟢 **100% Offline & Local, Zero Leakage** |
| **Standby RAM** | ~50MB - 100MB | 🔴 **200MB - 500MB** | 🟢 **< 10MB (Smart Idle Memory Reclamation)** |
| **Multi-Screen Staging** | ❌ Not Supported | ❌ Mostly Unsupported | 🟢 **Supported (Staged Drawer Capsule)** |
| **Startup / Response** | Network-dependent latency | Sluggish | 🟢 **Millisecond-Level Instant Response** |
| **Win11 Native Aesthetics** | Basic adaptation | Web-rendered feel | 🟢 **Fluent Ultra-thin Scrollbar / DWM Rounded Corners** |

---

## 🎬 Feature Demos

### 1. Instant OCR & Auto-Copy to Clipboard
Press the global hotkey (default `Ctrl + Alt + S`) to invoke the screenshot overlay. Selecting the text region automatically recognizes and copies the extracted text to the clipboard.

<p align="center">
  <img src="docs/images/1.gif" alt="Instant OCR & Auto-Copy" width="650" />
</p>

---

### 2. Multi-Region Continuous Selection
Supports selecting multiple snippets continuously within a single screenshot session, displaying index badges above top-left and aggregating recognized content in order.

<p align="center">
  <img src="docs/images/2.gif" alt="Multi-Region Selection" width="650" />
</p>

---

### 3. Cross-Application Staging & Capsule Drawer Insertion
Seamlessly capture screenshots across different applications and windows. Selecting "Switch Interface" automatically stages previous captures into the top capsule bar, keeping new canvases 100% clean. The dedicated "Insert" drawer enables thumbnail previewing, reordering with Move Up / Move Down, deleting, and `Ctrl + Z` undo history.

<p align="center">
  <img src="docs/images/3.gif" alt="Cross-Application Staging & Drawer Insertion" width="650" />
</p>

---

## ✨ Core Features

- 🔒 **100% Offline Local Inference**: Integrated PaddleOCR v6 engine with dynamic runtime switching between `PP-OCRv6_tiny` and `PP-OCRv6_small` models. Zero cloud dependencies, zero privacy risks.
- ⚡ **Smart Idle Memory Reclamation**: Automatically disposes of the inference engine and invokes Windows `EmptyWorkingSet` after 5 seconds of inactivity, reducing standby memory from ~40MB to **< 10MB**.
- 🖥️ **Multi-Monitor & DPI Adaptation**: Automatically traverses screen monitors and handles independent DPI scaling factors across multi-screen setups.
- 🎨 **Modern Windows 11 UI**: Native dark/light theme integration, DWM rounded corners, drop shadows, and 6px ultra-thin Fluent animated scrollbars.
- 🎛️ **Unified Control Center**: Unified Settings, About, and dual-source (GitHub / Gitee) update notifications with full English and Simplified Chinese bilingual support.
- ⌨️ **Productive Shortcut Ecosystem**:
  - `Ctrl + Alt + S`: Global screenshot OCR trigger (customizable in settings)
  - `Ctrl + Alt + C`: Open the Control Center
  - `Ctrl + C`: Copy result and close popup immediately
  - `Enter`: Launch query in default web browser
  - `Ctrl + Z`: Undo accidental deletions/reorderings in staged drawer

---

## 📂 Project Structure

```text
SnapFind/
├── src/        # Core source code (WPF / C#, UI, hotkey hooks & single-file publish)
├── libs/       # PaddleOCR C++ native dynamic dependencies & offline inference models
├── docs/       # Documentation assets and screenshots (docs/images/)
├── cache/      # Runtime user configuration (config.json)
├── releases/   # Auto-built installers (installers/) & portable packages (portables/)
└── backup/     # Project cleanup & temporary file removal scripts
```

---

## 🚀 Quick Start

### System Requirements
- Operating System: Windows 10 / Windows 11 (64-bit)
- Runtime: [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Installation & Usage
1. **Download**: Visit the [Releases Page](https://github.com/Loecedas/SnapFind/releases) to download the installer (`SnapFindSetup_vX.Y.Z.exe`) or portable zip (`SnapFindPortable_vX.Y.Z.zip`).
2. **Build from Source**:
   ```bash
   git clone https://github.com/Loecedas/SnapFind.git
   cd SnapFind/src
   dotnet run
   ```

---

## ❓ Frequently Asked Questions

**Q: Prompted with `DllNotFoundException` when running the portable version?**
> **A**: Ensure the `libs/` folder is located in the same directory as `SnapFind.exe`. It contains compiled C++ native binaries and inference models.

**Q: Global shortcuts do not respond?**
> **A**: The shortcut key may be occupied by another application. Right-click the system tray icon or press `Ctrl + Alt + C` to open the Control Center and customize your hotkeys.

**Q: Auto-start on boot does not work?**
> **A**: Auto-launch configures the registry run key under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Please verify your security software hasn't blocked the registry edit.

---

## 📄 License & Acknowledgments

- Distributed under the [MIT License](LICENSE.md) ([Chinese translation](LICENSE.zh.md)).
- Special thanks to [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) and [Inno Setup](https://jrsoftware.org/isinfo.php).

> **Disclaimer**: All OCR recognition is performed locally on your device. SnapFind will never upload screen content or extracted text to external servers.
