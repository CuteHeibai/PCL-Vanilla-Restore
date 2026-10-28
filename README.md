# PCL 原版下载恢复版

第三方基于 PCL 独立修改的版本。原作作者：**龙腾猫跃**。官方项目及赞助入口：[Meloong-Git/PCL](https://github.com/Meloong-Git/PCL)。本项目与 PCL 官方无隶属关系。

以官方 2.99.0.1 正式版 EXE 为输入，定向恢复原版 Minecraft 下载和安装，保留官方登录及已安装版本的组件管理。它不需要用户提供新的微软 Client ID。关于页保留原作者署名、赞助入口和原有购买劝导。

## 使用

从 Releases 下载最新 `PCL-Vanilla-Restore-*.exe`，放入可写目录后运行；也可重命名为 `PCL 原版下载恢复版.exe`。交付只需这个 EXE；缓存、帮助文件、游戏安装临时资源及更新文件会写入同目录的 `PCL` 文件夹。游戏目录可以在启动器中选择。

r3 起跟随官方正式版更新：查询官方 `最新正式版.zip` 的提交，下载对应固定提交的原包，在本机恢复 MC 下载、便携路径与下一次自动恢复能力，校验通过后再替换和重启。适配引擎、运行库与 dnlib 均嵌在 EXE 中，无需 SDK、编译器或配套 DLL。

r4 始终在主窗口右下角提供 **独立原版下载**。它直接读取 Mojang 官方版本清单，独立下载和校验客户端、Windows 依赖、资源与日志配置，生成 PCL 可识别的纯原版版本；不依赖 PCL 原下载页，也不处理账号。安装后返回启动页刷新版本列表，再由 PCL 登录和启动。

更新优先恢复原下载页；若只有下载页关键结构变化，而启动、便携路径及更新入口仍兼容，则自动改用独立下载模式。若基础接入也不兼容，拒绝更新并保留当前可用版本，其独立下载入口仍可使用。不能保证任意未来官方结构都能安全接入。官方包仅重新压缩而 EXE 未变化时，记住该次核对结果，避免重复提示下载。

更新暂存、备份、脚本及日志在 `PCL\Cache\Update`。使用 Windows 自带 PowerShell 替换原路径上的唯一启动器 EXE，不生成临时 PCL 更新 EXE。系统策略阻止辅助脚本时会取消更新并保留当前版本。

某些上游组件安装器要求英文或无空格路径。若启动器所在目录不满足要求，程序会提示移动目录，不把临时文件回退到系统目录。

## 构建和适配

Windows、.NET SDK 8 或更新版本、.NET Framework 4.8 开发引用程序集，以及内置 Windows PowerShell。

```powershell
./build.ps1 -PatchRevision 4
```

脚本从固定官方提交获取输入，校验其 SHA-256，编译适配工具、嵌入式更新模块和回归检查，输出 `artifacts/dist/PCL 原版下载恢复版.exe` 与供更新通道使用的同内容 `.bin` 文件。

输入绑定：

- 官方版本：2.99.0.1。
- 官方提交：`93ede5d9e638c99fcec853a9ba1eecd91ceee7f5`。
- 官方 EXE SHA-256：`0db34e19d9eeb6d496d20fabe786ec16bbd36ba6b0ccdc8d29cb7e909c114397`。

`src/AdaptationCore` 共享 net8/net48 适配引擎，`src/Patcher` 是固定输入的构建入口；`src/InjectedHooks` 的类型注入目标程序集。`src/RestoreRuntime`、适配引擎和 dnlib 均嵌入为资源并从内存加载，不在用户机器上释放额外 DLL。官方候选只作为数据解析，完成适配和验证后才安装执行。dnlib 使用 MIT 许可，许可文本随成品嵌入，另见 [docs/dnlib-LICENSE.txt](docs/dnlib-LICENSE.txt)。完整官方 EXE、反编译调查输出、账号数据与游戏文件不纳入源码仓库。

构建输入仍固定 SHA-256。运行时分别检查原生下载及基础接入的兼容性契约；原版登录、已安装版本组件管理及整合包限制的方法体必须在修改前后保持一致。每次适配的官方提交、原始哈希、下载模式和方法特征记录在成品的 `PCLRestore.Profile.json`；输入/成品哈希及模式另记在更新目录的 `adaptation.json`。基础结构不兼容时应更新规则并验收，不应关闭校验。

## 发布和更新接口

`update/stable.json` 保留给 r2 等旧客户端引导升级到 r4。r3/r4 的正常检查直接查询官方仓库，不等待本仓库为每个官方版本发布成品；已经安装 r3 的用户需手动下载 r4，才能获得本次新增的独立引擎。发布标签格式为 `2.99.0.1-r4`；Release 的 `PCL-Restore.bin` 与供用户分发的 EXE 字节相同，用户只需下载和分发 EXE。先发布资产，再更新稳定清单。

清单字段：`schemaVersion`、`upstreamVersion`、`patchRevision`、`artifactUrl`、`artifactSha256`，附官方来源提交和原始文件校验值。运行端校验版本、哈希、程序集身份及原版下载/便携路径/更新协议标记后才允许安装。

独立引擎与边界见 [docs/independent-vanilla.md](docs/independent-vanilla.md)，修改说明见 [docs/changes.md](docs/changes.md)，更新模块与回归覆盖详见 [docs/updater-report.md](docs/updater-report.md)，实际验收记录见 [docs/verification.md](docs/verification.md)，用户验收步骤见 [docs/acceptance.md](docs/acceptance.md)。可用 Windows PowerShell 执行 `tests/VerifyOfficialAutoRestore.ps1` 验证真实官方原包下载和连续两代本地恢复；`tests/VerifyIndependentInstall.ps1` 在隔离测试目录验证独立安装；`tests/VerifyPublishedRelease.ps1` 检查旧客户端引导通道。上游使用说明与分发要求见 [LICENCE](LICENCE)。
