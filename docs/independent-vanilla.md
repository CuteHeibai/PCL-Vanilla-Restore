# 独立原版安装（r5）

主窗口右下角“独立原版下载”支持搜索、类别筛选、自定义安装名、游戏目录及取消。仅安装原版；安装完成后回到 PCL 启动页刷新版本列表。默认游戏目录为 EXE 同目录的 `.minecraft`。

“独立”指窗口与安装规划独立于原下载页。r5 的版本列表、下载原理和镜像源直接使用当前 PCL 的原生实现，详见 [pcl-download.md](pcl-download.md)。不再保留 r4 的自建 HTTP 下载器或固定 8 文件并发策略。

## 更新选择

| 官方新版变化 | 处理 |
| --- | --- |
| 原下载页、基础接入及原生下载 API 兼容 | 恢复原下载页，同时保留独立入口 |
| 原下载页不兼容，基础接入及原生下载 API 兼容 | 使用 independent 模式，保留独立安装入口 |
| 基础启动/路径/更新或原生下载 API 不兼容 | 拒绝更新，保留当前可用 EXE |

两个模式均继承运行库和适配引擎。原生 API 的方法签名、目录加载器及内嵌文件校验器在适配前后检查；镜像策略方法体允许随官方更新。合成结构变化测试不代表任何未来官方版本都兼容。

## 文件和安装

安装规划处理标准客户端 JAR/JSON、Windows 库及 native classifiers、资源索引和 objects、旧版 virtual/map_to_resources、日志配置及纯原版 PCL 组件清单。PCL v1 列表可从 package URL 提取元数据 SHA1；没有 SHA1 的列表项要求 JSON 校验与版本 ID 匹配。客户端、库和资源等二进制文件必须有 SHA1，下载完成后仍复核。

候选镜像由 PCL 自己产生并排序，接受其 HTTPS 候选；外部元数据描述符仍限制来源。线程、限速、分段、重试、切源和缓存复用交给原生 NetFile/LoaderDownload，不维护第二套镜像替换规则。

运行库内嵌于单 EXE。安装暂存位于 `ExeDir/PCL/Cache/Vanilla`，原生下载分段暂存也沿用已便携化的 `ExeDir/PCL` 路径，不回退 Temp/AppData。游戏输出写入用户选择的目录。共享库、资源及日志文件直接交给原生引擎下载到最终共享路径；失败或取消时已完成文件可供后续校验复用。

名称拒绝路径穿越、保留名称和非法字符，已有同名版本拒绝覆盖。版本准备完成后，同盘通过目录移动发布；跨盘独占创建目录、跟踪自己创建的文件并完成元数据刷新。跨盘期间 PCL 可能暂时识别到未完成目录，安装完成后需刷新。普通失败/取消清理本次未完成版本；突然断电或强制结束进程不在恢复保证范围内。

安装器不读取账号、不更换 Client ID，不负责 Java 下载或认证。登录、Java 选择、启动和已有版本组件管理仍由 PCL 负责。

## 原生下载探针

```powershell
powershell.exe -NoProfile -NonInteractive -File tests/VerifyIndependentInstall.ps1 -VersionId 26.3 -Name Native-26.3
```

仅在 QA 副本替换入口，用真实 PCL 加载器进行安装；生产入口不变。默认游戏目录为 `artifacts/qa-full/.minecraft`，已有同名版本拒绝覆盖。`-SourcePreference 0` 临时测试镜像优先、`2` 测试官方优先，结束后恢复原下载源和版本列表设置；默认 `-1` 沿用原设置。实际记录见 [verification.md](verification.md)。
