# 富翁小镇 G5：本地集成检查与剩余阻塞

日期：2026-09-18。分支：`codex/rich-town-stride`。

**G4 已实现；G5 的审计与本机检查已落地，但 G5 出口未通过。** 不能把本分支称为完成真实 Host 集成、只读安装或完全自包含。
本轮仅执行 Debug 构建、单测、独立本机窗口和隔离部署尝试；没有 Windows CI、发布构建、正式 ZIP、统一 verify/seal，也未使用 AIFLOW。

## 1. 新增的复现入口

在插件仓库根执行；同一输出目录的构建和运行窗口应串行，避免 exe/DLL 锁定。

```powershell
pwsh -NoProfile -File scripts/Test-RichTownDevelopment.ps1
pwsh -NoProfile -File scripts/Test-RichTownDependencies.ps1
pwsh -NoProfile -File scripts/Test-RichTownDebugDeployment.ps1
pwsh -NoProfile -File scripts/Test-RichTownPlayableWindow.ps1
pwsh -NoProfile -File scripts/Test-RichTownLocalIntegration.ps1 -WritablePreview
pwsh -NoProfile -File scripts/Test-RichTownLocalIntegration.ps1
```

- [开发门禁](../../../scripts/Test-RichTownDevelopment.ps1)：全量既有回归、小镇测试、千种子模拟、G2/G3/G4 逐类覆盖率、资产重建、依赖摘要、格式和文档链接；默认 G5 新证据目录。
- [依赖审计](../../../scripts/Test-RichTownDependencies.ps1)：从实际 assets 和私有声明取包，核对 Standalone Debug 文件与 NuGet 源 SHA-256，审计普通/延迟 PE 导入及 DllImport 元数据。
- [隔离部署复现](../../../scripts/Test-RichTownDebugDeployment.ps1)：调用前核对 Build 实际目标目录，失败保留日志并返回错误；当前此项失败，不包含在默认通过的开发门禁内。
- [窗口循环](../../../scripts/Test-RichTownLocalIntegration.ps1)：中文/空格路径下的独立预览副本，2 次预热 + 20 次真实窗口创建/关闭，SDK 保存恢复、Restart、窄布局和最小化冻结；不复制到 Host、不冒充插件目录。
- 不带 `-WritablePreview` 时临时对**新建副本**施加只读 ACL，子进程实际尝试写入确认；finally 恢复 ACL。当前此检查明确失败，不能在默认入口删除只读要求。

预览进程的 `NUGET_PACKAGES` 指向空目录，报告记录实际托管/原生加载路径；这只证明本进程未从包缓存加载程序集，不能代替禁网、无开发工具的干净机器验证。
环境：Windows 10.0.26200、.NET 10.0.10、Avalonia 12.1.0、Stride 4.3.0.2507、win-x64；窗口报告所测 DPI scale=1。
本机枚举到 AMD Radeon RX 7700 XT / AMD Radeon(TM) Graphics，驱动 32.0.21030.2001；没有单独测显存，枚举设备不等同确认每一帧选用了哪块 GPU。

## 2. 原生与托管依赖

首轮成功审计：`artifacts/rich-town-tests/G5/dependencies-20260918-165807-642/dependencies.json`。
42 个私有包和 7 个 win-x64 原生文件全部与包源摘要一致；普通和延迟导入均解析，没有动态执行被审计文件。
PE 读取器放在 [开发脚本目录](../../../scripts/RichTown.NativeImports.cs)，只链接到测试，不进入插件；包含构造 PE 的正常、截断、坏 RVA、终止符、字符、架构和 DllImport 测试。

| 文件 | 非系统二级依赖 / 前提 |
| --- | --- |
| libstrideaudio.dll、freetype.dll、libstridevr.dll | `VCRUNTIME140.dll` 与 Windows UCRT API contracts |
| SDL2.dll、d3dcompiler_47.dll、openvr_api.dll、openxr_loader.dll | 审计到的静态导入为 Windows 系统组件/API contracts |

本机实际从 `C:\WINDOWS\SYSTEM32\VCRUNTIME140.dll` 加载 VC++ 运行库；它尚未随插件携带。
不能从本机 System32 复制 DLL 当作合法可再分发来源；后续需要 Microsoft 正式可再分发来源、许可、版本/摘要和干净环境原生加载验证。
动态 `LoadLibrary` / `GetProcAddress` 和 VR 驱动不由静态导入表穷尽；引擎未主动使用 VR 不等于可删掉锁定依赖。
现有 Kenney/Stride/SplitMix64 许可保留；所有传递组件的最终分发通知仍需补齐。审计中的 license 字段只是元数据，不能等同完成许可交付。

## 3. 实际窗口循环与资源曲线

首轮可写预览：`artifacts/rich-town-tests/G5/local-20260918-165931-855/local-result.json`。
67.47 秒完成两次预热 + 20 次开关。每次从同一公开 Restart 开局，经真实掷骰按钮在动画中 Capture/Accept，
最小化停止规则与绘制提交，恢复窗口后保持暂停；关闭释放 Document/视口，下一窗口通过 SDK 恢复并逐字比较存档内容。
每次关闭均检查本次新增的独立 Stride 临时缓存已删除。没有强制 GC 改善数字，没有把计时器调用数当作 GPU 帧率。

| 测量点 | 线程 | 句柄 | Private MiB | GDI / USER |
| --- | ---: | ---: | ---: | ---: |
| 两次预热关闭后 | 116 | 1715 | 346.7 | 21 / 42 |
| 再 20 次关闭后 | 114 | 1778 | 359.1 | 21 / 42 |
| 空闲约 3 秒后 | 113 | 1775 | 315.4 | 21 / 42 |

功能断言通过，但空闲后仍比预热多 **60 个句柄**，多段曲线接近逐轮增长，因此**资源稳定性未签署**。
不能把内存回落、无残留活动表面或 USER/GDI 稳定解释为没有泄漏。需要进一步定位内核句柄类型、拥有者及 Stride/驱动释放边界。
未测专用显存，也未测 150%/200% DPI、真实 Host Dock 浮动/重停靠/迁移中关闭。

最终构建再次复核：`artifacts/rich-town-tests/G5/local-20260918-171253-658/`，65.46 秒完成相同功能断言。
预热 / 20 次关闭 / 空闲后的句柄为 1709 / 1766 / 1763，空闲仍增加 **54**；线程 116 / 111 / 104，
Private MiB 348.1 / 365.5 / 324.6，GDI 均 21，USER 42 / 43 / 43。重复观察支持保留资源增长问题，未将本次功能通过当作稳定性签署。

## 4. 保留的失败与新发现

### 4.1 部署协议仍失败

`artifacts/rich-town-tests/G5/deploy-20260918-165931-591/deploy.log`：退出 1，Build 1.1.3 拒绝
`Microsoft.Extensions.DependencyModel.dll`。部署根经绝对路径检查，仅指向本轮 artifacts 下隔离 staging；没有生成有效插件目录。
没有删除传递依赖、修改缓存中的 Build targets、放宽 Host 共享策略或碰日常 Host 输出。
最终脚本复核：`artifacts/rich-town-tests/G5/deploy-20260918-171235-199/`，仍退出 1，实际目标 `staging/ClassicGamePlugin` 未生成。
所需跨仓方案：精确区分 Host 共享程序集身份与业务私有依赖，或给出完整且经过验证的 Host 共享闭包；独立版本化并验证 ALC 后再接回小镇。

### 4.2 当前原生视口仍有叠层限制

G1 已证实 HWND 盖住 Avalonia 叠层；本轮没有更换后端，因此历史失败仍有效。
已核对 Avalonia 12.1 的公开 GPU 导入/KeyedMutex 接口，以及 Stride 的 `SharpDXInterop`/`Texture.SharedHandle`，但 API 存在不证明集成已完成。
下一步最小实验必须核对设备 LUID、共享纹理格式、异步互斥交接、resize/关闭/设备丢失；不能在 UI 线程同步等待合成任务，也不能改为每帧 CPU 截图。
参考：[Avalonia 12.1 GPU 示例](https://github.com/AvaloniaUI/Avalonia/tree/12.1.0/samples/GpuInterop)、
[Avalonia GPU 交接约定](https://github.com/AvaloniaUI/Avalonia/issues/9925)、[Stride render texture 文档](https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/textures-and-render-textures.html)。

### 4.3 只读安装新增失败

`artifacts/rich-town-tests/G5/local-20260918-165807-819/local-result.json`：只读探针成功确认目录不可写，首个视口启动失败。
`Stride.Core.PlatformFolders` 的静态初始化在程序目录执行 `Directory.CreateDirectory("local")`，引发 UnauthorizedAccessException；
同一类型还会创建 `roaming`、`cache`。自定义 ObjectDatabase 放在 TEMP 不能阻止更早的引擎静态初始化。
公开字段为 static readonly；`ApplicationDataSubDirectory` 只控制 /data 挂载，不能覆盖这三个可写目录。
源码边界见 [PlatformFolders](https://github.com/stride3d/stride/blob/e023d874ba2985dbb132e9608f00d28d021b6f78/sources/core/Stride.Core/PlatformFolders.cs)。
本轮补充了启动中止清理：消息循环尚未建立时不调用 Stride Exit，仍完整 Dispose 游戏和窗口包装，避免附加的空引用掩盖根因。
最终只读复核 `artifacts/rich-town-tests/G5/local-20260918-171235-032/` 仍明确失败于 PlatformFolders；附加的 Exit 空引用已经消失，副本 ACL 已恢复。
后续需受支持的可写路径配置或独立、可审查的 Stride 修复版本；不能通过更改全局当前目录、反射修改 readonly 字段或偷偷预建 Host 目录来宣称通过。

## 5. 全量开发验证

最终完整开发门禁：`artifacts/rich-town-tests/G5/development-20260918-170855-433/`。

| 项目 | 实际结果 |
| --- | --- |
| locked restore / Debug 构建 | 通过，0 警告、0 错误 |
| 原有回归 | 555 + 1 个独立 Dispatcher 用例通过 |
| 小镇专用测试 | 221 通过；总计 **777 通过、0 失败、0 跳过** |
| G2 完整模拟 | 1,000 种子；235,074 命令，恢复续局 135,074 命令，单局最大 253 |
| G4 逐类覆盖率 | Document 98.38% 行 / 96.96% 分支；Codec、SaveTracker、SessionLease 均 100% / 100% |
| G2/G3 原有覆盖率门禁 | 全部通过；阈值未降低 |
| 素材重建与依赖审计 | mesh/RGBA 摘要一致；42 包、7 原生文件核对通过 |
| 格式/第一方差异/本地链接 | 通过；当次检查 14 份文档、191 个本地链接 |

最终文档补录后另行复核 14 份文档、193 个本地链接与 PowerShell 语法，均通过；没有为纯文档变化重复整套业务测试。

千种子终局摘要保持 `C43151292185F36A8C73428E1811B6C243D2220BDF350B9468A9E02F80029D53`，没有因 SDK 接线改变规则。
最终真实完整对局：`artifacts/rich-town-tests/G5/window-20260918-171023-167/`，83.5 秒，种子 19、30 轮、236 修订、73 条真人按钮命令、227 次跳过；
最终分数玩家 1=4395、玩家 2=1880、玩家 0=975，与 G3 基线一致。暂停/新对局/最小化/800 DIP 镜头按钮/最终排名/关闭释放均通过。
TRX、覆盖率和原始窗口报告在被忽略的 artifacts 下；克隆后须重跑脚本，本 Markdown 只记录实际结果，不冒充已随 Git 交付日志。
默认开发脚本不启动 GPU，不将 G1/G5 标记为通过。独立窗口脚本的通过仅指其中声明的功能断言。

## 6. 后续实施顺序

1. 独立确定 SDK/Build/Host 的依赖身份协议，补合规 Debug 暂存目录与 ALC 加载证据。
2. 选定并验证 GPU 合成后端，覆盖叠层、输入、resize、DPI、迟到回调及设备异常。
3. 解决 Stride 可写路径初始化，重跑默认只读检查；补 VC++ 合法来源、私有装载、许可与无缓存/禁网净机验证。
4. 定位循环后句柄增长，修复后重新采集预热、20 次、空闲曲线及显存。
5. 在隔离真实 Host 运行完整页面、保存恢复、Restart、Dock/焦点/DPI 矩阵；全部通过才签署 G1/G5。

用户日后另行授权发布时再执行发布流程。本阶段的局部通过与失败记录均保留，不将缺失验收降级成“完成”。
