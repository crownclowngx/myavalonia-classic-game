# 富翁小镇 G1：Stride 嵌入原型与阻塞记录

日期：2026-09-18。分支：`codex/rich-town-stride`。
基线 HEAD：`21457d24ee6b0f488b04dbef7e2694d2de229266`，从原 master 创建分支，保留用户已有的魔方、图标、SDK 等未提交改动。
工作树路径清单保存在 `artifacts/rich-town-tests/G1/baseline-status.txt`，该文件不是原始文件内容备份。

**结论：已实现 G1 最小原型，但 G1 不通过；G2–G5 未开始。**
Standalone 已在 Avalonia 页面内显示 Kenney 房屋；完整私有依赖部署和原生窗口叠层均出现真实阻塞。
本记录遵循[主方案](../../rich-town.md)的停止条件，不将单测通过或房屋截图当作真实 Host 集成完成。
本轮提交范围、Document 状态与后续全部阶段的工作包见[完整开发路线图](../../rich-town-roadmap.md)。

## 本轮实现

- `Features/RichTown`：独立 Document、G1 检查 View、部署声明。只有组合根新增登记，不调用其他游戏的任何领域/状态/动画代码。
- `ClassicGamePlugin.RichTown.Stride`：小镇专用渲染程序集，单向被 Plugin 引用；没有通用引擎服务、反向引用或新增 SDK 契约。
- `RichTownPrototypeViewport`：Avalonia NativeControlHost 创建 HWND；SDL foreign window 包装该句柄；UI Dispatcher 每约 33 ms 驱动一次引擎外部消息循环。
- `RichTownSurfaceController`：单表面租约、可见性/零尺寸过滤、初始化和帧故障清理、临时 Stop/Start 与最终 Dispose；不保存任何经济规则。
- `RichTownStrideSurface`：创建、物理像素 resize、消息循环和释放顺序。临时解绑先停帧并释放 SDL/GPU，再销毁 Avalonia 子 HWND。
- `RichTownPrototypeGame`：相机、光照、Kenney 房屋、调色板材质、显式 shader 数据库；不引入规则、物理或玩家输入控制器。
- 独立 `ClassicGamePlugin.RichTown.Tests`：资产损坏、边界、生命周期、并发租约、文档所有权及子领域依赖方向；不用显卡、不共享其他游戏测试工具。
- Standalone `--rich-town-probe` 仅包装 Plugin 中相同的 View/Document。普通窗口仍保持十四个标签页。

Module 当前为 **15 个普通 Document、15 个图标、23 条工作台命令**；第十五项明确显示“富翁小镇 3D（原型）”。
没有提前声明可玩游戏、存档、Restart 或 Undo。默认新增 Document 不启动设备，只有 View 挂上原生窗口时才初始化。
表面租约属于插件加载上下文，不是进程级跨插件隔离；正式对局会话和 G4 Scope 生命周期仍未完成。

## SOLID 与模式审查

| 原则 | 本轮代码证据 |
| --- | --- |
| S | Document 只适配 SDK 和最终所有权；控制器管理表面状态；Stride 适配器管设备；场景管图形；解析器只验证字节 |
| O | 生命周期依赖小镇自己的窄表面边界；故障替身无需修改真实 GPU 实现 |
| L | 表面替身与真实表面遵循相同启动/绘制/释放约定；真实窗口结果另行验证，不能由替身推定 |
| I | `IRichTownSurface` 仅 Start/Render/Rotate/Dispose；不加入其他游戏能力、存档或 Host 服务 |
| D | 控制器通过工厂接收表面，依赖图单向；边界测试禁止小镇和其他游戏互相引用 |

只使用组合和普通适配器，没有事件总线、Mediator、通用游戏框架或为了模式而建立的继承树。
新增实现注释用中文解释原生所有权、线程、DIP/像素、故障清理和素材转换。

## 两个停止条件已触发

### 1. 完整依赖声明无法通过当前插件部署协议

已显式声明 NuGet 图中的 42 个私有运行依赖包和 ProjectReference/资源/许可证。
执行前核验实际部署目标在本仓 `artifacts/rich-town-tests/G1/staging/ClassicGamePlugin` 内；未指向日常 Host。

```powershell
$root = [IO.Path]::GetFullPath('artifacts/rich-town-tests/G1/staging')
dotnet build src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj -c Debug --no-restore -warnaserror -p:SkipPluginDeploy=false "-p:ManagedPluginDeployRoot=$root"
```

退出码 **1**，Build 1.1.3 报告 `Managed Plugin 不得携带宿主共享程序集：Microsoft.Extensions.DependencyModel.dll`。
原始日志：`artifacts/rich-town-tests/G1/deploy-debug.log`。传递链和文件清单见[素材与依赖说明](../../rich-town-assets-and-dependencies.md)。
目标 Host Debug 编译为 0 警告、0 错误，但其输出不含 DependencyModel；本轮没有修改宿主源码、共享规则或构建包。
因为合规暂存目录尚未生成，**没有启动真实 Host 完成 ALC/Dock 验证**，也没有以整份 Standalone bin 冒充插件目录。

### 2. 原生子窗口遮住 Avalonia 叠层

检查入口：`dotnet run --project src/ClassicGamePlugin.Standalone -c Debug -p:SkipPluginDeploy=true -- --rich-town-probe`。
页面在视口上方放置 360×140 红色 Avalonia Border，文字为“叠层检查：红框必须完整盖住房屋”。
点击“切换叠层检查”后，按钮变为“关闭叠层检查（已开启）”，但红框被原生窗口遮住，仍然看见房屋。
这直接否定当前同窗口合成假设；Host 命令面板与 Dock 叠层的实际表现尚未验证，不能推定安全。
实际截图保存为 `artifacts/rich-town-tests/G1/overlay-enabled-but-obscured.jpg`，包含“已开启”按钮状态。

本轮只保留可复现原型，没有隐藏叠层、临时隐藏视口来冒充通过，也没有改成独立游戏窗口或 CPU 截屏贴图。
G1 下一步应评估 GPU 共享纹理接入 Avalonia 合成的成本，先解决设备/同步/释放/resize 的最小实验，再决定后端。

## 实际验证及边界

环境：Windows x64、.NET SDK 10.0.302、Stride 4.3.0.2507、插件 SDK 3.4.0 / Build 1.1.3、Standalone Avalonia 12.1.0。
目标 Host 使用 SDK 3.4.1、Avalonia 12.1.2；只确认其 Debug 编译，未签署混用兼容性。
本机列出 AMD Radeon RX 7700 XT、AMD Radeon Graphics 及远程显示设备，AMD 驱动 32.0.21030.2001；未采集实际被 Stride 选中的适配器或帧率指标。

| 项目 | 结果 |
| --- | --- |
| Standalone 嵌入房屋 | 已观察实际 GPU 场景及材质，非静态截图 |
| 按钮输入 / 单次视口重建 / 窗口关闭 | 已观察旋转反馈、重建后房屋重置、关闭退出；不等同于 20 次资源检查 |
| Avalonia 页面叠层 | 失败，详见上文 |
| 隔离 Debug 部署 | 失败，DependencyModel 禁带冲突 |
| 真实 Host ALC / 命令面板 / 浮动重停靠 | 未验证，部署前置条件未满足 |
| DPI 100/150/200%、失焦/最小化、零尺寸原生窗口 | 未完成真实矩阵；单测只覆盖零尺寸不提交绘制 |
| 连续 20 次开关、句柄/线程/内存/显存稳定性 | 未执行；不得填写为零增长 |
| 干净离线机、VC++、只读/中文路径 | 未验证 |
| Windows CI / AIFLOW / Release / ZIP / 发布门禁 | 未使用、未执行，未取得发布资格 |

### 单元测试与开发门禁

专用入口：`pwsh -NoProfile -File scripts/Test-RichTownDevelopment.ps1`。
执行 locked restore、Debug 警告即错误构建、全部测试、覆盖率采集、可复现资源转换、格式/链接/差异检查；默认不部署、不启动 UI。
最终本轮结果见下方追加记录。

首轮同进程 589 例：588 通过、1 失败；失败是既有魔方用例 `Document包装绑定标题与工作台重置仅作用于当前实例` 的 `Dispatcher.UIThread.RunJobs()`。
既有 556 例单独运行曾全部通过，随后解决方案混跑又出现 555 通过、1 失败，因此将其记录为测试运行基础设施的线程归属不稳定，不能宣称一直通过。
该原用例单独进程执行为 1 通过。当前脚本精确分区执行其余既有用例、该原用例、小镇测试，**没有删除用例或修改原断言**。
原始失败日志和 TRX 分别保存在 `unit-initial`、`split-tests`，独立结果在 `existing-games`、`isolated-binding`；不覆盖历史。
后续如要恢复既有所有 UI 用例同进程运行，应专门完善 UI 测试线程夹具，而不是在普通 Fact 中强行驱动全局 Dispatcher。

覆盖率对生命周期控制器、网格解析器与文档边界单独审阅；GPU、原生 HWND 和真实 Host 未覆盖的代码必须保留真实检查状态。
不设置发布覆盖率阈值，不排除业务代码美化结果，不以覆盖率文件代替完整窗口矩阵。

### 最终开发检查记录

命令：`pwsh -NoProfile -File scripts/Test-RichTownDevelopment.ps1`，退出码 **0**。
证据根：`artifacts/rich-town-tests/G1/development-20260918-125346-824/`，摘要为 `summary.json`。

| 检查 | 实际结果 |
| --- | --- |
| locked restore | 通过，所有锁文件一致 |
| Debug 全解决方案构建 | 通过，0 警告、0 错误，始终 SkipPluginDeploy=true |
| 原有测试（独立绑定用例之外） | 555 通过、0 失败、0 跳过；`existing/results.trx` |
| 原有魔方绑定用例独立进程 | 1 通过、0 失败、0 跳过；`existing-binding/results.trx` |
| 小镇专用测试 | 35 通过、0 失败、0 跳过；`rich-town/results.trx` |
| 合计 | **591 通过、0 失败、0 跳过**，不代表未经分区的旧测试进程问题已被根治 |
| 模型重新转换 | 7,044 顶点，mesh/RGBA 两份 SHA-256 与锁定清单一致 |
| C# 新增实现格式 | dotnet format whitespace --verify-no-changes 通过 |
| 文档链接 | 8 份同步文档的 82 个本地链接通过 |
| 差异空白 | git -c core.whitespace=cr-at-eol diff --check 通过 |

三个测试分区都采集了 Cobertura，各自位于对应结果目录下的运行 ID 子目录；没有将三份覆盖率简单相加。
小镇报告中，`RichTownSurfaceController` 与 `RichTownMeshData` 的行/分支覆盖率均为 **100%**；
`RichTownDocument` 为 96.42% / 93.75%，该报告未执行的静态 TypeId 初始化由原有组合契约测试使用。
`RichTownSurfaceLease` 分支为 100%，行覆盖率 66.66%，未执行静态 Shared 初始化；单测使用独立租约验证竞争，真实原型使用 Shared。
View、NativeControlHost、Stride 适配器和场景的单元覆盖率为 0，未从报告排除；本轮只做前文记录的真实窗口检查，剩余矩阵仍阻塞。

此前一次完整脚本 `development-20260918-125138-505` 的所有测试已通过，但差异检查发现 Standalone 项目文件末尾多余空行而返回 1；
修正空白后执行了上述最终检查，失败目录继续保留。最终检查后仅补充本节证据，另行复查文档链接与差异空白。

## 下一阶段前置条件

2026-09-18 路线图与提交前复核：新增完整路线图，补充 Document 现状、G1.1–G1.3、G2–G5 工作包和建议提交顺序，
同时将路线图链接检查及暂存区第一方差异检查接入专用开发脚本。重新运行脚本退出码 0：Debug 构建 0 警告/0 错误，
591 测试通过、0 失败/0 跳过，9 份文档 97 个本地链接通过，素材重建与格式检查通过。
本轮证据目录为 `artifacts/rich-town-tests/G1/development-20260918-130938-962/`，不覆盖上方历史记录。
此次新增的是路线图和提交检查，不是两个集成阻塞的修复；G1 状态仍为未通过。

1. 确定 DependencyModel 的公开合规交付方式，重新通过完整 Debug 暂存目录审计和真实 ALC 加载。
2. 处理原生窗口叠层或以新实验验证 GPU 纹理合成后端；保持小镇子领域独立，不修改其他游戏。
3. 补完 Dock/DPI/关闭与 20 次资源矩阵。G1 全部通过之后，才实施 G2 确定性规则和千种子对局测试。

本轮同步主方案、README、文档索引、项目职责、部署说明、G0 进度、素材许可与本专项记录。
