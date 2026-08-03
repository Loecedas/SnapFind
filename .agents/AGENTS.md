# SnapFind Workspace Rules

*   **发布包与目录存放规范**：
    *   根目录的每个文件夹必须存放对应的信息，禁止混淆与放错：
        *   `src/`：存放所有源代码及 `setup.iss` 打包脚本。
        *   `libs/`：存放 PaddleOCR 及 native C++ 原生动态依赖项。
        *   `cache/`：存放运行时配置（`config.json`）与临时调试图。
        *   `releases/installers/`：专门存放时间戳安装包 `SnapFindSetup_v1.0.0_yyyyMMdd_hhnn.exe`。
        *   `releases/portables/`：专门存放时间戳免安装 ZIP `SnapFindPortable_v1.0.0_yyyyMMdd_HHmm.zip`。
        *   根目录下只允许存放绿色启动的主可执行程序 `SnapFind.exe`，禁止放置其他依赖项。

*   **代码变更自动同步与打包触发机制**：
    *   **触发条件**：只有在代码或资源实际发生修改（如编辑 C# 文件、XAML 布局或项目依赖等）时，才触发打包。无代码变更时禁止打包。
    *   **同步要求**：一旦发生代码修改并完成编译发布后，**必须**同步执行以下三项操作：
        1. 同步将最新编译的 `SnapFind.exe` 拷贝至项目根目录下，覆盖旧的绿色版。
        2. 在 `releases/portables/` 中生成全新带时间戳的免安装 ZIP 压缩包。
        3. 在 `releases/installers/` 中生成全新带时间戳的安装包。
