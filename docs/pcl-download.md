# PCL 原生下载界面和实现

r6 在原下载位置恢复完整原生流程。与 r5 不同，没有自建安装规划或单独窗口。

| 功能 | 使用的 PCL 实现 |
| --- | --- |
| 导航、列表、选版本、命名和安装按钮 | `PageDownloadLeft`、`PageDownloadInstall` 及原 BAML/控件 |
| 版本列表与来源调度 | `ModDownload.DlClientListLoader` |
| 安装请求与任务 | `ModDownloadLib.McInstallRequest`、`McInstallStart` |
| 元数据/客户端候选源 | `DlSourceLauncherOrMetaGet` |
| 库、资源候选源 | `DlSourceLibraryGet`、`DlSourceAssetsGet` |
| 分段、校验、并发、限速、重试、取消 | 原生 `NetFile`、`FileChecker`、`LoaderDownload` 及 PCL 任务体系 |

版本列表使用 `ToolDownloadVersion`，文件源使用 `ToolDownloadSource`，遵守 PCL 的镜像优先、自适应和官方优先设置。当前候选策略包含 Mojang 官方源和 BMCLAPI 的 launcher/meta、maven/libraries、assets 映射；实际回退和文件复用由原生引擎处理。“镜像优先”不保证每个字节都来自镜像。

直接恢复失败时，备用恢复把当前可用 EXE 的已验证页面代码映射回候选原页面定义；安装仍走候选的原生 `McInstallStart`。接入兼容条件见 [independent-vanilla.md](independent-vanilla.md)。没有运行时的备用开关或额外下载入口。

实现依据固定原包 2.99.0.1，并对照提交 `93ede5d9e638c99fcec853a9ba1eecd91ceee7f5` 的 [ModDownload.vb](https://github.com/Meloong-Git/PCL/blob/93ede5d9e638c99fcec853a9ba1eecd91ceee7f5/Plain%20Craft%20Launcher%202/Modules/Minecraft/ModDownload.vb) 和 [ModNet.vb](https://github.com/Meloong-Git/PCL/blob/93ede5d9e638c99fcec853a9ba1eecd91ceee7f5/Plain%20Craft%20Launcher%202/Modules/Base/ModNet.vb)。历史 r5 的独立窗口和测试探针已移除，对应历史证据仍保存在 verification.md。
