# 富翁小镇 G3：本地可玩 3D 实施记录

日期：2026-09-18。分支：`codex/rich-town-stride`。工作树基线：G2 提交 `4c8f882`。

**已实现 G3 本地可玩版本；真实 Standalone 窗口的一真人两电脑完整对局通过。G1 集成仍阻塞，G4 SDK 存档/工作台 Restart 尚未完成。**
用户明确要求在当前分支继续 G3，因此本轮先完成本地玩法接线；不将原生叠层和依赖协议的历史失败改成通过。
本记录与[主方案](../../rich-town.md)、[完整路线图](../../rich-town-roadmap.md)共同维护；G2 规则与数值保持不变。

## 1. 实现范围与使用方式

从经典游戏的同一 `myavalonia.plugin.classic.game.document.rich-town` 入口打开“富翁小镇 3D（开发版）”。
目前真实 Host 部署仍被 G1 阻塞；开发预览执行：

```powershell
dotnet run --project src/ClassicGamePlugin.Standalone -c Debug -p:SkipPluginDeploy=true -- --rich-town-play
```

页面默认一名真人、两名电脑、24 格和 30 轮。真人掷骰，按落点购买或放弃；回合操作时可选择自己的地产升级或出售，然后结束回合。
等待偿债时，按详情选择可出售资产；破产、清算和终局排名继续由 G2 规则处理。电脑通过同一会话入口提交。
视口左键点击选择地块，拖动旋转，滚轮或按钮缩放，复位恢复默认镜头。右侧选择框提供不用精确点击 3D 的同等操作。
暂停冻结展示和电脑；隐藏、最小化后恢复需要明确点击“继续”。跳过动画只展示已经提交的结果。“新对局”生成新种子并使旧请求失效。

`--rich-town-probe` 继续打开独立 G1 诊断页面，保留房屋旋转、表面重建和叠层复现。
普通 Standalone 启动仍保留原有十四个标签页；游戏 View/Document 来自 Plugin，没有复制规则。

| 工作包 | 本轮交付 | 验证边界 |
| --- | --- | --- |
| G3.1 棋盘和实体 | 24 格带编号棋盘、三辆汽车、归属条和点数、选中边框 | 索引/坐标/骰子朝向单测；真实 GPU 场景 |
| G3.2 美术 | 既有 Kenney 房屋，一级一栋、二级两栋；其余实体程序生成 | 不新增下载、NuGet、原生库；原始资产摘要不变 |
| G3.3 镜头和输入 | 环绕、俯角/距离限制、缩放、复位、射线拾取 | 真实窗口鼠标检查；Host 焦点/DPI 矩阵仍在 G1 |
| G3.4 事件回放 | 骰子终态、逐格路径、交易反馈、单段有界回放 | 规则先提交；重复点击、跳过、隐藏、重开、关闭测试 |
| G3.5 中文页面 | 三位玩家、现金/地产、详情、最近动态、阶段操作、终局排名 | 宽窄布局、实际页面按钮、亮色主题可读性 |
| G3.6 完整体验 | 一真人两电脑完整对局与页面新对局 | 控制器多种子整局及真实窗口固定种子整局 |

## 2. 设计思路与 SOLID 审查

| 部件 | 职责和设计理由 |
| --- | --- |
| [RichTownPlayController](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Presentation/RichTownPlayController.cs) | Document 拥有的展示会话，组合 G2 Session、电脑延迟、单段回放和中文日志。UI 线程串行调用，不增加后台任务/取消竞态 |
| [RichTownPlayback](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Presentation/RichTownPlayback.cs) | 只读取一次已提交转换。时间是显式输入，可精确测试拐角、绕圈和暂停，无墙钟依赖 |
| [RichTownPresentation](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Presentation/RichTownPresentation.cs) | 中文状态、详情、日志和操作可用性。复用 G2 合法性；不维护第二套租金或金额公式 |
| [RichTownDocumentView](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Views/RichTownDocumentView.cs) | 构造 Avalonia 控件、绑定动作和管理当前视口订阅；不计算经济规则 |
| [RichTownSceneFrame](../../../src/ClassicGamePlugin.RichTown.Stride/RichTownSceneFrame.cs) | 不可变画面 DTO，以及可独立测试的棋盘/相机几何。只传坐标、编号和样式，不传领域命令或 SDK 对象 |
| [RichTownBoardScene](../../../src/ClassicGamePlugin.RichTown.Stride/RichTownBoardScene.cs) | 将 DTO 映射到 Stride 实体，处理视口内鼠标；静态网格一次创建，只有修订变化才重投影产权 |
| [RichTownStrideGame](../../../src/ClassicGamePlugin.RichTown.Stride/RichTownStrideGame.cs) | 引擎、模型/材质缓存、内容和 GPU 资源所有权；共用 G1 和 G3 的底层运行机制 |
| [RichTownViewport](../../../src/ClassicGamePlugin.RichTown.Stride/RichTownViewport.cs) | NativeControlHost、DIP 到物理像素、帧源、可见性及显式重建；沿用表面租约与故障收敛 |

- **S：** 价格、债务和胜负仍只在 Domain；View 不算钱，Renderer 不提交规则。页面和场景分离，诊断按钮留在专用 Probe View。
- **O：** G3 组合现有规则会话，不改 G2 算法。增加独立 `IRichTownPlayableSurface` 扩展画面/镜头能力，旧 `IRichTownSurface` 不被迫实现游戏指令。
- **L：** 真实和假表面遵守启动、停止、失败清理与取消订阅契约；缺少可玩接口会明确失败，不静默吞命令。
- **I：** 表面、可玩呈现、帧输入保持窄边界；没有万能游戏服务、事件总线、Mediator 或跨游戏框架。
- **D：** 规则不依赖图形，控制器不依赖窗口/驱动，渲染项目不反向引用 Plugin/SDK；GPU 通过表面接口替换，时间通过参数提供。

小镇所有业务位于 `Features/RichTown`，私有渲染库专供小镇；其他游戏无业务改动。Module 只更新小镇描述和显示名。
原有 15 个 Document、15 个图标、23 条工作台命令保持不变。没有因为页面“新对局”提前登记工作台 Restart。
没有为纯计算强行增加接口，也没有另设重复承担相同职责的 ViewModel。

## 3. 提交、动画与生命周期

1. 页面动作携带 `(Generation, Revision)`，控制器检查可见/暂停/动画/真人回合后提交 G2 Session。
2. Session 原子返回快照和事件；资金、位置和归属已经提交。日志最多保留 10 条，页面显示最近 6 条。
3. 一个回放对象展示骰子 0.4 秒、每格 0.16 秒及结束缓冲。没有无界动画队列，结束前不再提交下一条游戏命令。
4. 电脑延迟 0.35 秒，每帧最多一次命令；帧差最多使用 100 毫秒，不补跑隐藏期间的墙钟时间。
5. 跳过仅清空回放；新对局先验证快照再替换、递增 Generation；关闭停止控制器并清理视口/订阅，旧请求与旧表面选择事件均无效。

帧由 Avalonia UI 线程驱动，同一线程顺序更新控制器、画面和引擎，无等待 UI 的后台线程。
隐藏/最小化/零尺寸不渲染也不推进 AI；恢复需要继续。渲染失败停止表面并暂停会话，显式重试用最新快照恢复画面，不重新执行事件。
Document 持有会话，View 临时脱离只停止表面；换绑时解除旧文档/控制器/视口事件。最终 Dispose 幂等。
G3 仍只有表面单实例租约；正式激活级会话租约、保存取消与修订握手留到 G4。

## 4. 美术、窗口与依赖变化

房屋沿用 G1 已锁定的 Kenney mesh/RGBA 和 CC0 许可；汽车、车号、骰点、七段地块编号、公园和树木均是代码生成网格。
车身颜色和 1/2/3 个车顶标记对应玩家；地产也显示归属点数，避免仅靠颜色辨认。使用同一基础网格及按颜色缓存的模型，避免每帧创建 GPU 缓冲。
未下载 Car Kit/Roads，未新增依赖包或改锁文件，详见[素材依赖清单](../../rich-town-assets-and-dependencies.md)。

实际 .exe 预览最初因 Windows 兼容性 manifest 缺失无法创建 NativeControlHost 子窗口，失败日志保留在 `artifacts/rich-town-tests/G3/preview-1/`。
Standalone 增加 `app.manifest` 的 Windows 10 supportedOS 声明和 `asInvoker`，不要求管理员、不修改 Host。
另修复全局暗色主题与页面浅色背景混用导致的按钮文字对比不足；小镇页面采用局部亮色 ThemeVariantScope。

## 5. 自动测试与本地开发门禁

新增四组测试：

| 测试文件 | 关键断言 |
| --- | --- |
| [RichTownPresentationTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownPresentationTests.cs) | 规则先于动画、逐格转角/绕圈、跳过不重复付款、隐藏暂停、延迟 AI、旧请求、新对局、释放、债务/交易、四种子完整人机对局 |
| [RichTownGeometryTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownGeometryTests.cs) | 24 格闭环和同格偏移、六面骰目标朝上、骰点数量、镜头边界、两种比例 24 格拾取、无效输入、Standalone manifest |
| [RichTownPlayableSurfaceTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownPlayableSurfaceTests.cs) | 画面/镜头/输入转发、重建与旧事件、四种故障释放、不支持接口明确失败 |
| [RichTownViewTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownViewTests.cs) | 真实控件按钮和修订戳、买卖/升级可用性、暂停/跳过/新对局、宽窄布局、换绑后旧 Document 不影响新实例 |

```powershell
pwsh -NoProfile -File scripts/Test-RichTownDevelopment.ps1
```

该[开发脚本](../../../scripts/Test-RichTownDevelopment.ps1)默认 G3 新证据目录：锁定恢复、Debug 警告即错误、全部既有及小镇测试、1,000 种子模拟、覆盖率、资产转换摘要、格式、文档链接与差异空白。
八个 G2 关键类型仍各自要求行/分支至少 95%；G3 控制器/回放/中文展示/相机至少 95%，棋盘几何至少 90%。
几何的矩阵不可逆、平行射线等防御分支单独说明；不排除业务源文件提高比例，GPU/驱动代码用真实窗口补充。
原有魔方绑定用例继续独立测试进程执行，保留全部断言和精确计数，不删测试、不记作跳过。

首次全量检查 `development-20260918-161929-033/` 的 718 项测试均通过，但回放类分支覆盖率为 92.85%，未达到 95% 门禁。
补充终点停留和超时推进仍保持最终位置的断言，不降低阈值，最终复验已通过。
另一次 `development-20260918-162127-910/` 因窗口检查进程仍占用 Standalone.exe，构建复制失败；待该检查正常关窗后重新执行。
开发门禁与窗口检查应顺序运行，构建前关闭自己的预览窗口；此运行次序失败没有被记为通过。

最终证据根：`artifacts/rich-town-tests/G3/development-20260918-162158-174/`，脚本退出码 0。

| 检查 | 最终结果 |
| --- | --- |
| locked restore / Debug build | 通过，0 警告、0 错误，`SkipPluginDeploy=true` |
| 全量单测 | 718 通过 / 0 失败 / 0 跳过：既有 555 + 独立绑定 1 + 小镇 162（比 G2 新增 32 项） |
| G2 模拟 | 1,000 种子、235,074 条命令；单局最多 253，恢复后 135,074 条；与 G2 最终快照 SHA-256 一致 |
| G2 关键类型 | 八个类型均达到行/分支 95%；Rules 为 97.82% / 97.67%，其余均 100% / 100% |
| 素材重建 | 7,044 顶点；mesh 和 RGBA SHA-256 与锁定清单相同 |
| 格式 / 文档 / 差异 | 通过；脚本当次检查 156 个本地链接，后续只补充结果文字并单独复查文档 |
| PowerShell 脚本 | 两个开发入口语法解析通过；窗口脚本检查进程退出码和结果报告 |

| G3 关键类型 | 行覆盖率 | 分支覆盖率 | 门禁 |
| --- | --- | --- | --- |
| RichTownPlayController | 100% | 98.16% | ≥95% |
| RichTownPlayback | 100% | 100% | ≥95% |
| RichTownPresentation | 100% | 98.55% | ≥95% |
| RichTownBoardLayout | 100% | 94.11% | ≥90% |
| RichTownCamera | 100% | 100% | ≥95% |

剩余分支主要为可选事件订阅的空分支、产权日志的清算映射，以及棋盘拾取的不可逆矩阵/平行射线/身后交点防御。
G2 破产资产回收由规则测试覆盖；本轮不宣称真实窗口已人工遍历全部清算、坏驱动或 DPI 场景。

TRX 位于证据根的 `existing/results.trx`、`existing-binding/results.trx`、`rich-town/results.trx`；覆盖率附件位于各套件运行子目录。
机器汇总见 `summary.json`、`simulation.json`、`rules-coverage.json`、`presentation-coverage.json`。未执行的真实 Host/发布事项仍明确写为未签署。

## 6. 真实窗口证据与人工检查

显式执行[本机窗口检查](../../../scripts/Test-RichTownPlayableWindow.ps1)：

```powershell
pwsh -NoProfile -File scripts/Test-RichTownPlayableWindow.ps1
```

仅本机 Windows 桌面/GPU 使用；不属于 Windows CI，也不由无窗口开发脚本自动启动。
它启动自己的 Debug Standalone，固定 seed 19，以实际页面 Button/ComboBox 操作真人回合，正常电脑仍由控制器驱动。
测试驱动根据只读快照选择真人命令，不更改真实会话中的真人标志、不绕过页面提交入口；前几步正常动画，后续用页面跳过加速。
它验证暂停/重复点击/新对局、最小化冻结与显式继续、800 DIP 布局、镜头按钮、终局排名和关窗后的 Document 释放。
目录每次新建，报告不覆盖；失败、超时或视口错误立即使本次检查失败。普通预览不会自动操作或写报告。

2026-09-18 实测证据：`artifacts/rich-town-tests/G3/window-20260918-160254-133/window-result.json`。
环境为 Windows 11 企业版 10.0.26200、.NET SDK 10.0.302、锁定 Stride 4.3.0.2507 / Avalonia 12.1.0。
系统枚举到 AMD Radeon RX 7700 XT 与 AMD Radeon 集成显卡，驱动 32.0.21030.2001；未测量显存或声明多显卡切换通过。

| 项目 | 实际结果 |
| --- | --- |
| 完整对局 | 通过，1 真人 / 2 电脑，30 轮，修订 236，73 条真人页面命令 |
| 动画跳过 | 227 次；不重复提交规则 |
| 最终排名 | 小蓝 4,395 第一，小橙 1,880 第二，真人 975 第三 |
| 生命周期/布局 | 暂停、新对局、重复点击、最小化恢复、800 DIP、镜头按钮、关窗释放通过 |
| 时间和进程 | 83.69 秒；进程正常退出，stderr 为空；这不是性能基准 |

窗口脚本补充退出码检查后，在 `window-20260918-162017-463/` 再次通过，82.30 秒，修订、73 条真人命令和最终排名与首轮一致，stderr 为空。

人工补充检查在同机实际 Standalone 窗口执行：亮色文字可读，点击 04 号街区详情与边框一致，拖动改变环绕视角，滚轮缩放，复位恢复；掷出 3 后车辆到休息格且骰子顶面 3 点。
继续操作中，机会奖励使现金增加 200，税务站扣除 100；在 14 号地购买/升级/出售后，现金依次为 1,600 → 1,450 → 1,300 → 1,450，产权条与房屋同步出现/清除。
同场景可观察电脑的一栋/两栋房屋等级变化。上述人工预览使用随机开局，不作为固定种子自动报告的一部分。
完整对局驱动不会验证真实鼠标输入，所以以上交互另行观察，不用纯几何单测替代。截图仅在检查会话中观察，本轮没有承诺已保存 PNG 文件。

## 7. 未解决事项与后续路线

- **G1 依赖协议：** Build 1.1.3 禁带 `Microsoft.Extensions.DependencyModel.dll`，Host 又缺该依赖；没有合规暂存目录或真实 ALC 成功证据。
- **G1 原生叠层：** HWND 会遮挡 Avalonia 叠层；真实 Host 弹窗、Dock、DPI 100/150/200%、20 次开关和 GPU 资源观察尚未完成。
- **G4：** 在同一 Document 加入公开 SDK 保存/恢复、坏存档与保存修订竞争、工作台 Restart、正式单会话激活边界。页面新对局不等同这项交付。
- **G5：** 合流处理离线/只读/中文路径、原生二级依赖、稳定性和集成验收；不把 Standalone bin 成功称为插件自包含。

同步主方案、路线图、阶段总记录、README/索引、项目职责、资产及部署说明；G1/G2 历史结果保留原记录。
本轮未使用 AIFLOW、Windows CI、Release 构建、正式打包、统一 verify/seal 或发布门禁，没有修改宿主加载策略。
