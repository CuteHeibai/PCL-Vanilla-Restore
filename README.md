# PCL 原版下载恢复版

第三方基于 PCL 独立修改的版本。原作作者：**龙腾猫跃**。官方项目及赞助入口：[Meloong-Git/PCL](https://github.com/Meloong-Git/PCL)。本项目与 PCL 官方无隶属关系。

以官方 2.99.0.1 正式版 EXE 为输入，定向恢复原版 Minecraft 下载和安装，保留官方登录及已安装版本的组件管理。它不需要用户提供新的微软 Client ID。关于页保留原作者署名、赞助入口和原有购买劝导。

## 使用

从 Releases 下载最新 `PCL-Vanilla-Restore-*.exe`，放入可写目录后运行；也可重命名为 `PCL 原版下载恢复版.exe`。交付只需这个 EXE；缓存、帮助文件、游戏安装临时资源及更新文件会写入同目录的 `PCL` 文件夹。游戏目录可以在启动器中选择。

更新使用已经适配并验证的完整修改版。官方发布了新版本并不代表它立即可更新；未适配版本不会覆盖当前修改版。程序不会回到官方更新通道。

更新暂存、备份、脚本及日志在 `PCL\Cache\Update`。使用 Windows 自带 PowerShell 替换原路径上的唯一启动器 EXE，不生成临时 PCL 更新 EXE。系统策略阻止辅助脚本时会取消更新并保留当前版本。

某些上游组件安装器要求英文或无空格路径。若启动器所在目录不满足要求，程序会提示移动目录，不把临时文件回退到系统目录。

## 构建和适配

Windows、.NET SDK 8 或更新版本、.NET Framework 4.8 开发引用程序集，以及内置 Windows PowerShell。

```powershell
./build.ps1
# 同一上游版本的后续修订：
./build.ps1 -PatchRevision 2
```

脚本从固定官方提交获取输入，校验其 SHA-256，编译适配工具、嵌入式更新模块和回归检查，输出 `artifacts/dist/PCL 原版下载恢复版.exe` 与供更新通道使用的同内容 `.bin` 文件。

输入绑定：

- 官方版本：2.99.0.1。
- 官方提交：`93ede5d9e638c99fcec853a9ba1eecd91ceee7f5`。
- 官方 EXE SHA-256：`0db34e19d9eeb6d496d20fabe786ec16bbd36ba6b0ccdc8d29cb7e909c114397`。

`src/Patcher` 使用 dnlib 修改副本；`src/InjectedHooks` 的类型注入目标程序集；`src/RestoreRuntime` 的 DLL 和用于读取候选元数据的 dnlib 均嵌入为资源并从内存加载，不在用户机器上释放额外 DLL，也不加载执行候选程序。dnlib 使用 MIT 许可，许可文本随成品嵌入，另见 [docs/dnlib-LICENSE.txt](docs/dnlib-LICENSE.txt)。完整官方 EXE、反编译调查输出、账号数据与游戏文件不纳入源码仓库。

新增上游版本需要新增明确的输入绑定和适配实现，并通过隔离验收；不要直接放宽输入校验。所有修改方法的原始特征记录在成品的 `PCLRestore.Profile.json` 资源中。

## 发布和更新接口

更新清单位于 `update/stable.json`。发布标签格式为 `2.99.0.1-r1`；Release 中的 `PCL-Restore.bin` 是适配成品，和用户下载的 EXE 内容一致。先上传经过验证的版本和更新数据，再更新稳定清单。

清单字段：`schemaVersion`、`upstreamVersion`、`patchRevision`、`artifactUrl`、`artifactSha256`，附官方来源提交和原始文件校验值。运行端校验版本、哈希、程序集身份及原版下载/便携路径/更新协议标记后才允许安装。

更新模块与回归覆盖详见 [docs/updater-report.md](docs/updater-report.md)，实际验收记录见 [docs/verification.md](docs/verification.md)。上游使用说明与分发要求见 [LICENCE](LICENCE)。
