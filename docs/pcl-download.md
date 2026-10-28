# 直接复用 PCL 下载实现

r5 按用户要求复用宿主 PCL 的真实下载代码。独立窗口只负责选择版本与规划纯原版安装，不复制一套网络引擎或镜像排序算法。

| 功能 | 调用的 PCL 实现 |
| --- | --- |
| 版本列表与来源调度 | `ModDownload.DlClientListLoader`，读取 `DlClientListResult.Value/SourceName` |
| 客户端、元数据、索引、日志候选源 | `DlSourceLauncherOrMetaGet` |
| 库候选源 | `DlSourceLibraryGet` |
| 资源 objects 候选源 | `DlSourceAssetsGet` |
| 文件任务、校验与批量下载 | `ModNet.NetFile`、`MeloongCore.FileChecker`、`ModNet.LoaderDownload` |
| 等待、取消、进度 | 原生 `WaitForExit`、`Cancel`、`Progress` |

版本列表使用 `ToolDownloadVersion`，文件源使用 `ToolDownloadSource`，分别遵守 PCL 的镜像优先、自适应、官方优先设置。当前 PCL 的列表来自 Mojang/BMCLAPI；文件候选包含官方和 BMCLAPI 的 launcher/meta、maven/libraries、assets 映射。最终切源与复用由原生引擎处理；“镜像优先”不表示每个字节都一定来自镜像。

全局并发、限速、分段、重试及 CDN 处理沿用 PCL。共享依赖一次提交给原生批量任务，保留真实 libraries/assets 路径以支持其已有文件复用。元数据、客户端等分步任务也使用同一引擎。

实现对照了固定原包 2.99.0.1 的实际方法与上游提交 `93ede5d9e638c99fcec853a9ba1eecd91ceee7f5` 的 [ModDownload.vb](https://github.com/Meloong-Git/PCL/blob/93ede5d9e638c99fcec853a9ba1eecd91ceee7f5/Plain%20Craft%20Launcher%202/Modules/Minecraft/ModDownload.vb) 和 [ModNet.vb](https://github.com/Meloong-Git/PCL/blob/93ede5d9e638c99fcec853a9ba1eecd91ceee7f5/Plain%20Craft%20Launcher%202/Modules/Base/ModNet.vb)。适配时检查原生 API 和内嵌 FileChecker 的签名；接口不兼容就保留旧版。候选源来自可信宿主策略，要求 HTTPS，不另设固定镜像域名列表阻止官方新增镜像。

验证包含真实官方优先 1.8.9、BMCLAPI 优先 1.5.2、GUI 26.3 安装，以及源选择、校验、取消、已完成共享文件保留、API 变化拒绝等回归。逐项证据与边界见 [verification.md](verification.md)。
