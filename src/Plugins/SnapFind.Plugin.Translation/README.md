# SnapFind 翻译插件 (SnapFind.Plugin.Translation)

SnapFind 独立开箱即用的现代化翻译与繁简转换插件，专为 WPF 及 .NET 8+ Windows 桌面应用设计。

---

## 插件特性

- **纯粹独立**：零第三方三方包依赖，仅包含翻译与转换核心功能，不携带任何 OCR、截图或额外业务。
- **在线网络翻译**：
  - 基于微软 Edge 翻译通道，支持包括中、英、日、韩、法、德、俄、西、意、葡、阿、越、泰等主流语种。
  - 内置动态感知 Windows 系统代理与 VPN (`DynamicSystemWebProxy`)，网络变动无需重启即可生效。
  - 连接池敏捷回收 (`SocketsHttpHandler`)，网络瞬断自动重试恢复。
- **本地离线繁简转换**：
  - 基于 Windows 原生 Win32 API (`LCMapString`)，毫秒级本地极速转换，断网亦可完美运行。
- **独立现代化 UI 窗口**：
  - 现代化圆角界面设计，深色/浅色自适应主题，无边框设计及 Windows 11 原生圆角支持。
  - 内置高可靠防抖（600ms）实时键入翻译、快捷键支持（`Ctrl+Enter` 立即翻译、`Esc` 退出）。
  - 内置多轮重试原生剪贴板复制保护。
- **遵循窗口规则**：固定尺寸设计，禁用随意拖拽拉伸边框，保持 UI 紧凑精致。

---

## 快速接入指南

### 方式一：直接添加项目引用（推荐）

在另一个项目中编辑 `.csproj`，添加对本插件的项目引用：

```xml
<ItemGroup>
  <ProjectReference Include="..\path\to\SnapFind.Plugin.Translation\SnapFind.Plugin.Translation.csproj" />
</ItemGroup>
```

### 方式二：直接引用已编译 DLL

1. 运行 `build-plugin.ps1` 编译产物，在 `dist/` 目录下获取 `SnapFind.Plugin.Translation.dll`。
2. 将 `SnapFind.Plugin.Translation.dll` 拷贝至目标项目的 `libs/` 或相应依赖目录。
3. 在目标项目的 `.csproj` 中添加引用：

```xml
<ItemGroup>
  <Reference Include="SnapFind.Plugin.Translation">
    <HintPath>libs\SnapFind.Plugin.Translation.dll</HintPath>
  </Reference>
</ItemGroup>
```

---

## 代码调用示例

在目标项目引入命名空间：
```csharp
using SnapFind.Plugin.Translation;
```

### 1. 弹出独立翻译窗口

可以在托盘图标菜单、快捷键或工具栏按钮的点击事件中调用：

```csharp
// 打开翻译窗口（支持传入初始文本和父级窗体）
TranslationPlugin.ShowWindow("Hello, World!", this);

// 或者通过实例调用
TranslationPlugin.Instance.OpenTranslateWindow("需要翻译的内容");
```

### 2. 在业务中异步调用翻译 API（如在 OCR 结果框中实时翻译）

```csharp
// 自动检测源语言并翻译为简体中文
string result = await TranslationPlugin.Translate("Hello, how are you?", fromLang: "auto", toLang: "zh-CN");

// 翻译为英语
string enResult = await TranslationPlugin.Translate("你好，世界", fromLang: "auto", toLang: "en");
```

### 3. 本地极速繁简字转换

```csharp
// 简体转繁体（完全离线）
string traditional = TranslationPlugin.ConvertToTraditional("简体中文转为繁体中文");

// 繁体转简体（完全离线）
string simplified = TranslationPlugin.ConvertToSimplified("繁體中文轉為簡體中文");
```

### 4. 获取支持的语言列表（用于绑定 ComboBox 下拉列表）

```csharp
foreach (var lang in TranslationPlugin.Instance.SupportedLanguages)
{
    myComboBox.Items.Add(new ComboBoxItem
    {
        Content = lang.DisplayName,
        Tag = lang.Code
    });
}
```

### 5. 自定义配置（可选）

```csharp
TranslationPlugin.Instance.Configure(new TranslationOptions
{
    DefaultTargetLanguage = "zh-CN",
    DebounceDelayMs = 500
});
```

---

## 目录结构说明

```
src/Plugins/SnapFind.Plugin.Translation/
├── SnapFind.Plugin.Translation.csproj  # 项目工程文件 (.NET 8 WPF 类库)
├── ITranslationPlugin.cs               # 插件标准接口定义
├── TranslationPlugin.cs                # 插件门面类与静态便捷调用入口
├── TranslationEngine.cs                # 核心翻译引擎（微软网络翻译）
├── ChineseConverter.cs                 # Win32 原生离线繁简转换器
├── DynamicSystemWebProxy.cs            # 动态注册表代理感知器
├── LanguageInfo.cs                     # 语言元数据定义
├── TranslationOptions.cs               # 插件选项配置模型
├── PluginClipboard.cs                  # 高可用剪贴板安全写入
├── PluginThemeHelper.cs                # 主题深浅模式适配器
├── TranslateWindow.xaml                # 现代化独立翻译交互窗口布局
├── TranslateWindow.xaml.cs             # 翻译交互窗口后台代码
├── build-plugin.ps1                    # 独立一键编译与产物输出脚本
└── README.md                           # 本说明文档
```
