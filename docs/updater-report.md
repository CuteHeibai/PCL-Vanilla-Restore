# 官方更新与本地恢复

r3 正常检查直接读取 `Meloong-Git/PCL` 中最近修改 `最新正式版.zip` 的提交，通过固定提交的 raw URL 下载原包。`update/stable.json` 仅供 r2 等旧客户端引导升级，r3 不以该清单作为正常官方更新源。

## 下载与适配

1. 查询官方提交；与当前嵌入来源相同则结束。手动更新总是先询问；后台默认也询问。
2. 将官方 ZIP 下载为 `PCL/Cache/Update/official-{commit}.zip.bin`，仅提取根目录的 `Plain Craft Launcher 2.exe` 到 `official-source.bin`。拒绝重名、缺失、过大和截断数据；不提取其它路径。
3. 比较程序集版本与原始哈希，拒绝降级和相同输入；允许同版本官方重新构建。确认无需更新的官方提交与当前成品哈希绑定保存，避免每次启动重复下载。换 EXE 或出现新提交时该记录失效。
4. 内存加载 `PCLRestore.Runtime`、`PCLRestore.Adaptation` 和固定 dnlib 4.5.0；不释放 DLL，不依赖 SDK，不执行未适配原包。运行端与构建端共用适配代码。
5. 对关键方法进行兼容性比较。规则规范化版本字符串与内联构建号，保留调用、字段、分支、指令及异常处理结构；入口必须唯一。结构变化则拒绝适配，保留当前程序。
6. 恢复原版列表、导航、纯原版新建安装、便携路径、更新入口及下一代自动恢复模块。写出后检查官方登录/组件管理/整合包限制的方法体和 Client ID 未发生意外改变，官方资源未改变，并校验原版列表、纯原版限制、依赖与来源标记。
7. 输出 `candidate.bin`，绑定源提交、原包 SHA-256、成品 SHA-256 与方法特征。事务使用本机算出的成品 SHA-256 再校验。

自动兼容不等于支持任意未来版本。测试中的新版版本号为合成样本，不能证明未来官方结构一定兼容。关键结构变化需要更新适配规则并重新验收。官方来源依赖 GitHub HTTPS 与固定提交 URL；新版本原包没有预先发布在本仓库的哈希白名单。

所有更新数据在 EXE 同目录的 `PCL/Cache/Update`，包括官方压缩包、原始程序、候选、备份、脚本、锁、事务、就绪信息和日志。`adaptation.json` 记录输入/输出身份。没有外部临时目录回退，也不生成临时 PCL 更新 EXE。

## 替换和恢复

沿用 r2 的生产事务：通过独占锁和会话/PID 握手启动系统 Windows PowerShell。脚本隐藏启动，不覆盖系统执行策略；脚本未能启动时保持原程序运行。

脚本等待调用进程退出，再次检查会话及原文件/候选哈希，将原 EXE 备份为 `previous.bak`，把 `candidate.bin` 移到原来同名 EXE 路径，然后启动该路径。主窗口加载完成后必须回报对应会话及成品哈希；默认等待 90 秒。失败时尝试恢复原字节并重启，只能终止本次事务自己启动的失败进程。已存在的备份不会因最初文件占用而丢失。

成功后仅原路径有一个启动器 EXE；原包/缓存/备份的扩展名为 `.bin` 或 `.bak`。突然断电及持续权限/磁盘故障仍可能妨碍恢复，不能保证所有环境下回退成功；日志记录备份位置。

可选 `settings.json`：

```json
{
  "checkAutomatically": true,
  "promptBeforeInstall": true
}
```

缺失或无效设置采用以上默认值。`promptBeforeInstall: false` 允许后台直接下载、适配并安装通过验证的官方包。上游的检查时机与开关仍然保留。

## 验证

r3 完整构建通过 21 项二进制检查、8 项自动适配测试和 52 项更新回归，0 警告、0 错误。

覆盖原有 32 项清单/元数据/事务测试，以及官方元数据、固定来源 URL、严格 ZIP 提取、大小与截断限制、未更新记录、版本排序、本地恢复、结构不兼容拒绝、游戏文件保留和下一代恢复能力。事务测试使用真实 Windows PowerShell，覆盖正常替换、损坏候选、文件占用、锁冲突、启动失败、错误会话/哈希和超时回退；测试脚本中的启动探针仅用于夹具，生产代码没有测试开关。

```powershell
./build.ps1 -PatchRevision 3
powershell.exe -NoProfile -NonInteractive -File tests/VerifyOfficialAutoRestore.ps1
```

后者从实时官方接口下载原包，调用真实嵌入式模块适配，验证下一代仍可再次适配，且依赖仅从内存加载。它只写入 `artifacts/official-integration`，不启动用户游戏。真实界面替换验收及边界见 [verification.md](verification.md)。

## r4 independent fallback

r4 always exposes the embedded independent installer through the main-window button. The original contract is split into native-download and essential-host checks. Native restoration remains preferred; only a native mismatch can fall back to independent mode after every host/path/update anchor still matches. A host mismatch retains the currently working EXE. Both output modes inherit the runtime/adapter and downloader. `vanillaMode` is recorded in the profile and adaptation journal; manual current-version messages distinguish independent mode.

The new engine consumes official Mojang metadata with mandatory SHA1 validation, Windows library/native rules and asset/logging plans, stages below ExeDir/PCL/Cache/Vanilla, and publishes standard versions plus a pure component manifest. It never handles account tokens. See independent-vanilla.md for finalization, cross-volume and cancellation behavior.

The r4 full build passes 24 binary, 10 adaptation and 65 updater/installer checks. Real standalone installation and GUI fallback evidence is recorded in verification.md. The original production PowerShell replacement transaction is unchanged.
