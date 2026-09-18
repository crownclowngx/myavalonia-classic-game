# 富翁小镇 G4：SDK Document 与存档

日期：2026-09-18。分支：`codex/rich-town-stride`。在 G3 提交 `ae03d46` 上继续实现。

**实现已完成，SDK 契约测试通过；真实 Host 文件对话框、原子写入和 Dock 集成仍受 G1/G5 前置问题阻塞。**
本阶段没有修改 Host、SDK、包版本、锁文件或其他游戏的业务。没有运行发布检查。

## 1. 交付与职责

| 类型 | 单一职责与设计理由 |
| --- | --- |
| [RichTownDocument](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/RichTownDocument.cs) | 公开 SDK 激活、关闭、保存握手与 Restart 适配；组合下面的窄职责对象，不计算租金或操作文件 |
| [RichTownContentCodec](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Persistence/RichTownContentCodec.cs) | 独立 DTO 与 schema 1；先完整校验再构造状态，领域 C# 类型变化不会自动变成文件协议变化 |
| [RichTownSaveTracker](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Persistence/RichTownSaveTracker.cs) | 锁内配对不可变内容和 DocumentRevision，编码在锁外；规则修订归零不影响保存修订单调递增 |
| [RichTownSessionLease](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/RichTownSessionLease.cs) | 插件 ALC 内只允许一个已激活小镇，所有权 token 原子获取/归还，失败者不能释放成功者 |
| [RichTownPlayController](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Presentation/RichTownPlayController.cs) | 复用 G2/G3 会话和动画；Restore 与 Restart 使用同一状态替换路径，代数递增使旧动作失效 |

采用普通组合、不可变快照和一个小锁，没有新增事件总线、通用游戏框架或跨游戏适配服务。
Document 实现 `IPersistablePluginDocument`、`IWorkbenchDocumentCommandTarget`、`IDisposable`。
SDK 3.4.0 的 `IDocumentLifetime` 由带 `ActivatorUtilitiesConstructor` 的公开构造接收；Standalone 使用无参或测试构造，窗口负责释放。
关闭信号只使保存/命令失效并通知 SDK，不从后台触碰 GPU。下一 UI 帧停止玩法推进，最终 Dispose 释放表面及激活租约。

SOLID：规则与持久化/渲染分责（S）；新增恢复能力不改 G2 规则（O）；遵守 SDK 取消、修订和释放约定（L）；保存/命令/表面使用各自窄契约（I）；依赖公开 SDK 和不可变内容（D）。

## 2. 稳定身份与注册事实

- Document：`myavalonia.plugin.classic.game.document.rich-town`，沿用 G1 身份。
- Restart：`myavalonia.plugin.classic.game.command.rich-town.restart`。
- 菜单：`myavalonia.plugin.classic.game.command-placement.menu.tools.rich-town.restart`。
- 14 个普通 Document + 1 个持久化小镇 = 15 个 Document，15 个图标。
- 24 条命令 / 24 条 Tools 菜单 = 15 条 Restart + 9 条 Undo；现有 2 条五子棋快捷键不变。

小镇不借用其他游戏的 `WorkbenchDocumentCommandAdapter`；命令身份和状态都由本子领域拥有，Module 只做声明注册。
页面“新对局”和工作台 Restart 走同一入口。没有小镇 Undo，也没有新的默认快捷键。
激活前、取消关闭后和 Dispose 后 Restart 不可用；状态通知精确携带本命令 ID，不逐帧通知 Host。

## 3. 文件内容协议

Host 负责路径、扩展名、信封、原子文件写入、备份及错误交互；插件只返回 SDK `DocumentContent`。
schema 1 的 Payload 保存：地图 `rich-town-ring24-v1`、规则版本、随机算法 `SplitMix64-v1` 与 16 位十六进制完整状态，
三名玩家、18 块地产、轮次/当前玩家/阶段、升级标记、债务与终局原因、规则修订/事件序号、最后骰面和是否掷过骰。
待购买地产由当前阶段和当前位置唯一确定；不另存可能矛盾的地块编号。排名从完整终局状态重新计算。
不保存镜头、选中格、动画进度、计时器、控件、任务或 GPU 资源。

输入上限 32,768 个 JSON 字符，最大深度 8；未知/重复/缺失字段、非法枚举、null 元素、错误数量、未知地图/算法均拒绝。
随后执行完整 G2 不变量验证，额外固定一真人两电脑与骰面范围；计数远超正常 30 轮上限时拒绝，避免伪造溢出。
恢复先解码，再获取激活租约，再替换；失败不改变原始会话、不发布标题、不占有租约。
恢复后干净、暂停、无旧动画，点击“继续”才推进电脑；不会因磁盘读取耗时补跑回合。

### 动画中保存的设计调整

G0 原计划在动画结束后才开放保存。G3 实际已采用“规则原子提交后才回放动画”，因此 G4 可以在动画中捕获已提交的完整快照。
这是实现条件得到证明后的调整：保存不等待 GPU、不跳过动画、不再次结算；恢复直接显示已提交位置与余额。
`DocumentRevision` 与规则 Revision 分开：例如捕获修订 1 → 买地变成 2 → 收到 1 的写盘确认，仍然脏；Restart 后规则回到 0，保存修订继续增长。
仅确认当前且实际捕获过的修订才清脏，旧确认、未来确认和重复确认无副作用。暂停/选中格/动画完成不会制造内容修订。

## 4. 测试与门禁

[存档负例](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownPersistenceTests.cs)覆盖全部五个阶段、随机续序、重复字段/超大内容/未知版本、SDK 内容冻结与 28 类坏字段。
[Document 测试](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownDocumentPersistenceTests.cs)覆盖：

- 动画中捕获、保存后继续操作、旧局确认、未来确认、重复确认、保存修订与内容的并发配对。
- 16 个并发激活只有一个成功；失败者释放不影响获胜者；隐藏仍占租约；关闭及释放异常归还租约。
- 恢复后暂停与确定性续局；重复初始化、未知创建入口、坏存档、初始化预取消。
- 公开 DI 构造、SDK 关闭信号、已关闭迟到保存确认、Restart 身份与取消、旧动作无效。

首个定向成功结果：`artifacts/rich-town-tests/G4/unit-third/`，209 通过、0 失败、0 跳过。
Codec / SaveTracker / SessionLease 行与分支均 100%；Document 行 98.34%、分支 96.96%。
开发脚本新增以上四类**各自 95% 行/分支门禁**；完整解决方案回归与最终证据见 [G5 记录](g5-local-integration.md)。
早期 DI 构造歧义已由显式构造标注及测试解决，未修改 SDK 契约来绕开问题。

## 5. 使用方式与限制

通过 Host 正式打开该 Document 后，保存/另存/恢复由 Host 文档服务接管；当前部署阻塞，因此没有声称已操作过真实 Host 的保存文件对话框。
Standalone `--rich-town-play` 继续复用正式 Document/View，没有新增一份自定义文件保存器。
G5 显式窗口检查通过同一 SDK Capture/Accept/Restore 和真实页面做反复关闭恢复，但不替代 Host 原子写入测试。
后续必须在合规插件目录生成之后，补实际 Host 保存、关闭询问、恢复、命令路由和 Dock 生命周期验证。
