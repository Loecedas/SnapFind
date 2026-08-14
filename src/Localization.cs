using System;

namespace PixOcrSearch
{
    public static class Localization
    {
        public static event Action? LanguageChanged;

        public static string CurrentLanguage => ConfigManager.Current.Language ?? "zh-CN";

        public static bool IsEnglish => string.Equals(CurrentLanguage, "en-US", StringComparison.OrdinalIgnoreCase);

        public static void SetLanguage(string lang)
        {
            if (ConfigManager.Current.Language != lang)
            {
                ConfigManager.Current.Language = lang;
                ConfigManager.Save();
                LanguageChanged?.Invoke();
            }
        }

        // --- Settings Window / Control Center ---
        public static string ControlCenterTitle => IsEnglish ? "SnapFind Control Center" : "SnapFind 控制中心";
        public static string TabSettings => IsEnglish ? "Settings" : "设置";
        public static string TabUpdates => IsEnglish ? "Updates" : "版本";
        public static string TabAbout => IsEnglish ? "About" : "关于";

        public static string LabelLanguage => IsEnglish ? "Language" : "界面语言";
        public static string LangChinese => "简体中文";
        public static string LangEnglish => "English";

        public static string LabelSearchEngine => IsEnglish ? "Search Engine" : "搜索引擎";
        public static string SearchGoogle => IsEnglish ? "Google" : "谷歌 (Google)";
        public static string SearchBing => IsEnglish ? "Bing" : "必应 (Bing)";

        public static string LabelOcrModel => IsEnglish ? "OCR Model" : "OCR 识别模型";
        public static string OcrModelTiny => IsEnglish ? "PP-OCRv6 (Tiny - Fastest)" : "PP-OCRv6 (轻量版 - 速度最快)";
        public static string OcrModelSmall => IsEnglish ? "PP-OCRv6 (Standard - High Accuracy)" : "PP-OCRv6 (标准版 - 准确率高)";

        public static string LabelScreenshotHotkey => IsEnglish ? "Screenshot Hotkey" : "全局截图快捷键";
        public static string LabelControlPanelHotkey => IsEnglish ? "Control Center Hotkey" : "控制面板快捷键";
        public static string HotkeyTooltip => IsEnglish ? "Click box and press keys to set" : "点击文本框后直接按键盘设置";
        public static string NoHotkey => IsEnglish ? "None" : "无快捷键";

        public static string CheckAutoStart => IsEnglish ? "Start with Windows" : "开机自动启动";
        public static string CheckAutoCopy => IsEnglish ? "Auto copy to clipboard after OCR" : "识别后自动复制到剪贴板";

        public static string BtnCancel => IsEnglish ? "Cancel" : "取消";
        public static string BtnSave => IsEnglish ? "Save" : "保存";

        public static string MsgModifierRequired => IsEnglish
            ? "Hotkeys must include modifier keys (such as Ctrl, Alt, Shift) to prevent conflicts with normal typing!"
            : "快捷键必须包含修饰键 (如 Ctrl, Alt, Shift 等)！以防键盘普通按键被系统全局拦截冲突。";
        public static string TitleModifierRequired => IsEnglish ? "Invalid Setting" : "设置无效";

        public static string MsgHotkeyConflict => IsEnglish
            ? "Screenshot hotkey and Control Center hotkey cannot be the same! Please use different combinations."
            : "截图快捷键与控制面板快捷键不能相同！请设置不同的快捷键组合以防冲突。";
        public static string TitleHotkeyConflict => IsEnglish ? "Hotkey Conflict" : "快捷键冲突";

        public static string MsgAutoStartFailed => IsEnglish ? "Failed to configure startup:\n" : "设置开机启动失败:\n";
        public static string TitleWarning => IsEnglish ? "Warning" : "警告";
        public static string TitleError => IsEnglish ? "Error" : "错误";
        public static string TitleInfo => IsEnglish ? "Info" : "提示";

        // --- Updates Panel ---
        public static string UpdateTitleLatest => IsEnglish ? "Release Notes" : "最新更新日志";
        public static string UpdateTitleNew(string ver) => IsEnglish ? $"New Version Available: v{ver}" : $"发现新版本: v{ver}";
        public static string UpdateTitleUpToDate => IsEnglish ? "You're up to date" : "当前已是最新版本";
        public static string UpdateProgressPercent(int pct) => IsEnglish ? $"Download Progress: {pct}%" : $"下载进度: {pct}%";
        public static string UpdateConnecting => IsEnglish ? "Connecting to server..." : "正在连接服务器...";
        public static string UpdateDownloading => IsEnglish ? "Downloading update..." : "正在下载更新...";
        public static string UpdateDownloadFailed => IsEnglish ? "Download failed: " : "下载失败: ";
        public static string BtnDownloadNow => IsEnglish ? "Download Now" : "立即下载";
        public static string BtnInstallNow => IsEnglish ? "Install Now" : "立即安装";
        public static string BtnIgnoreVersion => IsEnglish ? "Ignore Version" : "忽略此版本";
        public static string BtnNotNow => IsEnglish ? "Later" : "暂不更新";

        // --- About Panel ---
        public static string AboutVersion(string ver) => IsEnglish ? $"Version: v{ver}" : $"版本: v{ver}";
        public static string AboutIntroTitle => IsEnglish ? "About & Disclaimer" : "项目简介与声明";
        public static string AboutIntroText => IsEnglish
            ? "SnapFind is a high-performance, fully-local screenshot OCR & search tool tailored for Windows.\nAll captured images and OCR inferences run entirely on your local machine and are never uploaded to the cloud, ensuring complete privacy."
            : "SnapFind 是一款专为 Windows 系统度身定制的高性能、纯本地截图 OCR 识别与搜索工具。\n所有截取的图片与识别结果完全保存在本地进行推理，绝不上传云端，彻底保护您的隐私安全。";
        public static string AboutUsageTitle => IsEnglish ? "How to Use" : "操作方法";
        public static string AboutUsageText => IsEnglish
            ? "• Screenshot: Press the hotkey to select a screen area, release to instantly perform OCR.\n• Quick Copy: In the result card, press Ctrl + C to copy the text and auto-close the card.\n• Web Search: Press Enter or click 'Search' to search the text in your default browser."
            : "• 截图：按快捷键选定屏幕区域后松开，即刻唤起 OCR 识别结果。\n• 快速复制：在结果文本卡片中，直接按 Ctrl + C 拷贝内容，软件会自动拷贝到剪贴板并关闭窗口。\n• 网页检索：按 Enter 键或点击结果框中“搜索”按钮，拉起浏览器一键检索。";
        public static string AboutLinksTitle => IsEnglish ? "Official Links" : "官方渠道";
        public static string AboutGithubLabel => IsEnglish ? "GitHub: " : "GitHub 地址: ";
        public static string AboutWebsiteLabel => IsEnglish ? "Website: " : "官方网站: ";
        public static string BtnCheckUpdate => IsEnglish ? "Check for Updates" : "检查更新";
        public static string CheckingUpdates => IsEnglish ? "Checking..." : "正在检查...";
        public static string CheckingUpdatesStatus => IsEnglish ? "Checking for updates..." : "正在检查更新...";
        public static string BtnClose => IsEnglish ? "Close" : "关闭";
        public static string MsgAlreadyLatest(string ver) => IsEnglish
            ? $"You are already using the latest version (v{ver})."
            : $"您当前已是最新版本 (v{ver})，无需更新。";
        public static string MsgNoReleaseFound => IsEnglish
            ? "No release information found on GitHub or Gitee. Please check your network connection."
            : "未能在 GitHub 或 Gitee 检测到发布版本，请检查您的网络连接或稍后再试。";

        // --- Edit Window (OCR Result Card) ---
        public static string EditResultTitle => IsEnglish ? "OCR Result" : "OCR 识别结果";
        public static string BtnSettings => IsEnglish ? "Settings" : "设置";
        public static string BtnCopy => IsEnglish ? "Copy" : "复制";
        public static string BtnSearch => IsEnglish ? "Search" : "搜索";
        public static string MsgCopyFailed => IsEnglish ? "Failed to copy to clipboard:\n" : "复制到剪贴板失败:\n";
        public static string MsgSearchFailed => IsEnglish ? "Failed to launch default browser for search:\n" : "无法启动默认浏览器进行搜索:\n";
        public static string TitleSearchFailed => IsEnglish ? "Search Failed" : "搜索失败";

        // --- Tray & App ---
        public static string TrayToolTip => IsEnglish ? "SnapFind - Screenshot OCR & Search" : "SnapFind - 截图 OCR 搜索";
        public static string TrayMenuScreenshot => IsEnglish ? "Screenshot OCR & Search" : "截图 OCR 搜索";
        public static string TrayMenuControlPanel => IsEnglish ? "Control Center" : "控制面板";
        public static string TrayMenuExit => IsEnglish ? "Exit" : "退出";
        public static string MsgAlreadyRunning => IsEnglish ? "SnapFind is already running in the background!" : "SnapFind 已经在后台运行中！";
        public static string MsgHotkeyRegisterFailed(string mods, string key, string cpMods, string cpKey) => IsEnglish
            ? $"Failed to register global hotkeys.\nScreenshot: {mods} + {key}\nControl Center: {cpMods} + {cpKey}\nPlease adjust in Settings to avoid conflicts."
            : $"无法注册全局快捷键。\n主截图快捷键: {mods} + {key}\n控制面板快捷键: {cpMods} + {cpKey}\n请在设置中修改，避免与其他程序冲突。";
        public static string MsgScreenshotFailed => IsEnglish ? "Failed to start screenshot:\n" : "启动截图失败:\n";
        public static string MsgOcrInitFailed(string ex) => IsEnglish
            ? $"PaddleOCR engine initialization failed:\n{ex}\n\nPlease ensure the \"libs\" directory and its dependent DLLs and inference models are intact."
            : $"PaddleOCR 引擎初始化失败:\n{ex}\n\n请确保 \"libs\" 目录及其中的依赖 DLL 和 inference 文件夹完整。";
        public static string MsgOcrRecognitionFailed(string ex) => IsEnglish
            ? $"An error occurred during OCR recognition:\n{ex}"
            : $"OCR 识别发生错误:\n{ex}";
        public static string TitleOcrError => IsEnglish ? "OCR Error" : "OCR 错误";
    }
}
