# 项目、Host 与 Standalone 窗口职责

## 三个基础项目与小镇专用项目如何分工

| 项目 | 应当负责 | 不应负责 |
| --- | --- | --- |
| `ClassicGamePlugin.Plugin` | `IPluginModule`、View、Document/Tool Model、业务服务和插件私有资源 | 启动独立桌面程序、引用 Host 内部实现 |
| `ClassicGamePlugin.Standalone` | 启动 Avalonia、承载 Plugin 中的真实界面、提供开发期 Stub | 成为第二套插件实现或模拟完整 Host |
| `ClassicGamePlugin.Tests` | 验证业务、初始化、状态隔离、注册和生命周期约定 | 代替真实 Host 的最终加载验收 |
| `ClassicGamePlugin.RichTown.Stride` | 仅供小镇使用的原生表面、Stride 场景、内容与资源释放 | 引用其他游戏、SDK、Dock 或承载经济规则 |
| `ClassicGamePlugin.RichTown.Tests` | 小镇规则/债务/随机/会话/电脑、完整对局、资产校验、子领域边界和生命周期 | 借用其他游戏的状态或假装执行真实 GPU/Host 验收 |

只有 `.Plugin` 提供插件入口；它的显式私有运行依赖可以随插件交付。Standalone 可执行程序、开发 Stub 与 Tests 不随插件发布。

## 插件如何融入主项目

`ClassicGamePluginModule.Configure` 是组合入口。它通过 `IPluginRegistration`：

1. 登记插件私有服务；
2. 用稳定 Descriptor 声明 Document、Tool 等贡献；
3. 把 Model 与 View 的对应关系交给 Host。

Host 读取构建生成的 `plugin.manifest.json`，检查 Plugin SDK 兼容区间，加载唯一的 `IPluginModule`，再按
登记结果创建 Document Scope、Tool singleton、View 和 Dock 适配对象。插件不应扫描 Host、直接操作
Host 容器，或保存 `IPluginRegistration` 供运行时使用。

## Standalone/MainWindow 应当做什么

Standalone 的 `MainWindow` 是开发工作台，不是插件对 Host 暴露的正式窗体。它应当保持轻薄，只负责：

- 启动 Avalonia 并加载与插件一致的主题和必要资源；
- 创建或解析 Plugin 中的真实 Model，把真实 View 放入窗口并设置正确的 `DataContext`；
- 为插件确实需要的 Host Port 提供显式、可识别的开发 Stub；
- 在插件贡献增多时，扩展成简单的 Document/Tool 浏览工作台，但继续复用 Module 的登记事实。

当前 `MainWindow` 使用十四个标签页分别创建扫雷、蜘蛛纸牌、黑白棋、五子棋、围棋、中国象棋、2048、数独、推箱子、俄罗斯方块、空当接龙、消消乐、中国跳棋和三阶魔方 Document，并承载各自的
SDK 边界包装 View。十四个包装 View 都通过单向绑定把 Document 拥有的 ViewModel 交给真正的游戏 View。该结构用于快速
检查全部游戏的布局与交互；规则、计时和状态转换仍全部来自 Plugin 项目，Standalone 不维护第二份实现。窗口关闭时只释放
确实拥有计时、动画、后台电脑、题目生成或求解资源的 Document；数独、空当接龙与中国跳棋会取消后台工作并停止计时，2048、推箱子、俄罗斯方块与消消乐的动画或游戏循环
计时器只存在于视觉树中，这四个 Document 都没有外部资源，不增加空洞的释放行为。

三阶魔方 Document 拥有后台教学求解和串行动作播放器；关闭时取消求解并停止播放，3D 控件在隐藏或脱离视觉树时
暂停当前角度、停止帧源并退订事件。恢复显示后由用户继续播放。详细职责和开发检查见[三阶魔方专用文档](rubiks-cube.md)。

它不负责证明以下行为：

- manifest 发现、Plugin SDK 兼容检查和程序集加载上下文；
- 真实 Dock 布局、Document Scope、Tool singleton、保存恢复和关闭语义；
- Host 生命周期、卸载、安装、升级或 ZIP 加载。

这些行为必须在真实 Host 中验收。不要为了让 Standalone 看起来像 Host 而复制 Host 源码或维护第二份
贡献清单。

## 富翁小镇 3D 的 G4 持久化版本（G5 集成仍阻塞）

G2 已在 Plugin 的 `Features/RichTown/Domain` 与 `Application` 实现纯规则和串行会话，见[G2 专项记录](plan-history/rich-town/g2-deterministic-rules.md)。
规则不依赖 UI/引擎/SDK。G3 已由 Document 拥有 `Presentation/RichTownPlayController`，组合会话、事件回放、电脑延迟和中文投影。
View 只负责控件和订阅；渲染类库只接收不可变画面 DTO，不能反向执行经济规则。详见[G3 专项记录](plan-history/rich-town/g3-playable-scene.md)。

[富翁小镇专用方案](rich-town.md)已实现原型 Document 和私有 `ClassicGamePlugin.RichTown.Stride` 类库。
新增内容全部归小镇子领域，渲染库单向被 Plugin 引用，不引用 Plugin 或任何其他游戏；没有通用游戏引擎服务。
现有十四个游戏保持独立，Module 登记同一个持久化小镇 Document；14 普通 + 1 持久化、15 图标、24 条工作台命令。
G4 已接入 schema 1、保存修订、关闭取消、激活级租约和 Restart；页面“新对局”复用同一 SDK 命令入口。
文件路径/事务属于 Host，DTO 校验属于小镇 Codec，修订握手属于 SaveTracker，详见 [G4 记录](plan-history/rich-town/g4-document-and-save.md)。

当前 Document 先停止展示会话，再负责视口最终释放；View 中的 NativeControlHost 负责临时解绑/重建，解绑保留 Document 对局。
UI 线程帧源顺序驱动可控时间回放和 SDL 消息循环，隐藏/零尺寸/最小化暂停，恢复需要明确继续。
私有表面控制器处理失败和租约。Standalone 的 `--rich-town-play` 承载 Plugin 同一游戏 View/Document；`--rich-town-probe` 承载专用诊断 View，共用适配器。
显式 `--rich-town-play-check <报告路径>` 只供本机开发窗口检查，操作同一页面按钮，不复制规则；默认预览不会自动运行。
独立窗口已完成一真人两电脑整局；部署仍被构建协议阻止，页面叠层历史失败保留，真实 Host/Dock/DPI 的完整矩阵未通过。
显式 `--rich-town-integration-check <报告路径>` 检查 2 次预热 + 20 次真实开关及 SDK 内容恢复，代码只在 Standalone。
正式激活租约保持到 Document 关闭，表面租约只限制 GPU；两者均只属于小镇。
部署冲突、只读初始化失败及循环后句柄增长见 [G5 集成记录](plan-history/rich-town/g5-local-integration.md)。
当前不运行 Windows CI、统一 Gate、Release 构建、正式 ZIP 或发布门禁；不使用 AIFLOW。

## 项目原则

1. **稳定身份优先。** Plugin、Document 和 Tool ID 发布后保持稳定；显示名、类名和目录名可以演进。
2. **一份业务与界面事实。** View、Model、服务和 Module 都放在 Plugin 项目，Standalone 只负责承载。
3. **只依赖公开边界。** 使用 Plugin SDK、UI SDK 和声明式注册，不引用 Host 内部程序集或 Dock 实现。
4. **遵守生命周期。** Document Model 与局部服务属于每次打开的 Scope；Tool Model 属于插件级 singleton。
5. **明确缺失能力。** Standalone 缺少 Host Port 时使用显式 Stub 或显示“不支持”，不要静默传入 `null`。
6. **保持包边界干净。** Host 共享的 SDK、Avalonia、Dock 和 `Microsoft.Extensions.*` 不进入插件目录。
7. **让构建生成事实。** manifest、依赖闭包和正式 ZIP 交给 Build 包，不手工维护或压缩 `bin`。
8. **逐层验证。** 单元测试、Standalone、干净部署目录、正式 ZIP、真实 Host 五层验证不能互相替代。
