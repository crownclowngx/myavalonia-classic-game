# 富翁小镇 G2：确定性规则、会话与完整对局

日期：2026-09-18。分支：`codex/rich-town-stride`。工作树基线：`686d7f7`。
阶段结束时状态：**G2 开发验证通过；当时 G1 集成仍阻塞，G3/G4 页面与存档未实施**。

后续进展：用户已授权并实施 [G3 本地可玩版本](g3-playable-scene.md)，Document 会话与页面新对局已接通；下文保留 G2 当时的范围及测试事实。

用户明确要求在当前分支继续 G2。原路线的 G1 前置顺序据此调整：纯规则独立推进，G1 部署和叠层阻塞仍保留，
正式 G3 玩法接入仍要求 G1/G2 都通过。本轮没有创建新分支，没有运行窗口、部署或发布流程。

关联：[主方案](../../rich-town.md)、[完整路线图](../../rich-town-roadmap.md)、[阶段总记录](g0-design-and-development-plan.md)、[G1 历史结果](g1-stride-integration.md)。

## 1. 实际交付与未实现边界

- 实现 24 格、3 人、整数金额、最多 30 轮；掷骰、起点奖励、机会/税费、购买/放弃、租金、两级升级和出售。
- 实现固定债务、出售偿债、自动破产清算、出局跳过、最后存活者获胜、30 轮账面总值排名及并列。
- 实现不可变快照、版本固定的可恢复随机源、原子规则转换、有序事件、实例级串行会话与普通电脑决策器。
- 新增规则/经济/负例/并发/恢复测试和 1,000 种子完整模拟，扩展专用 Debug 本地门禁与子领域结构检查。
- 保留现有原型 Document、View、稳定 ID、15 个 Document/图标和 23 个工作台命令；不修改其他游戏业务。

**尚未实现：** G3 的 3D 棋盘/动画/中文玩法页面，G4 的 Document 会话所有权、SDK 存档 DTO/保存握手、Restart/取消代数，
以及 G1/G5 的真实 Host、ALC、Dock/DPI/离线与资源稳定性验证。构造规则快照可恢复不等于已有 SDK 存档。
当前原型页面仍显示 G1 房屋，不会因 G2 类存在而自动成为可玩页面。

## 2. 文件与职责

所有规则只属于 `Features/RichTown`；没有抽取供其他游戏使用的公共游戏层，也没有新增项目、NuGet 或非托管依赖。

| 文件 | 职责与设计理由 |
| --- | --- |
| [RichTownBoard.cs](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Domain/RichTownBoard.cs) | 唯一数值配置、24 格地图、4 张机会卡、租金和估值；规则版本为 1 |
| [RichTownState.cs](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Domain/RichTownState.cs) | 不可变玩家/地产/债务/快照、开局和排名；`ImmutableArray` 不泄露可变集合 |
| [RichTownSnapshotValidator.cs](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Domain/RichTownSnapshotValidator.cs) | 校验编号、范围、业主、阶段、债务、终局、规则/随机版本及修订关系 |
| [RichTownRandom.cs](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Domain/RichTownRandom.cs) | 显式状态输入/输出的 SplitMix64-v1 与无偏有界取样 |
| [RichTownCommands.cs](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Domain/RichTownCommands.cs) | 简单命令、拒绝原因、请求与有序只读事件；不是消息总线 |
| [RichTownRules.cs](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Domain/RichTownRules.cs) | 校验命令并计算完整下一状态；局部 `Turn` 仅在一次命令中存在 |
| [RichTownSession.cs](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Application/RichTownSession.cs) | 持有唯一已提交状态，用实例锁串行调用纯规则；锁内不通知 UI 或调用外部回调 |
| [RichTownComputerPlayer.cs](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Domain/RichTownComputerPlayer.cs) | 只从快照提出同一入口的命令，不写状态、不窥视随机、不安排延迟 |

以上类型均为插件内部类型，不将规则类型或 Stride 类型增加到公共 Plugin SDK。
快照校验保证可继续计算的结构不变量，不宣称验证整条历史真实性；G4 仍需独立处理不可信文件和版本迁移。

## 3. 提交与阶段契约

`RichTownSession.Submit(request)` 接受期望修订、当前玩家和命令。成功使 `Revision` 加一，事件序号连续递增；
调用方一次收到不可变快照和全部事件。会话只保存最新快照，不持有无界事件历史。
拒绝返回原快照引用、空事件和明确原因；不推进随机状态、现金、回合、修订或事件序号。
恢复快照不合法时构造会话抛参数异常，不改动任何现有会话。极端金额或序号溢出拒绝整个命令。

| 当前阶段 | 允许的命令 |
| --- | --- |
| 等待掷骰 | Roll |
| 等待购买 | Buy / Decline；Buy 只针对当前落点，不携带地块参数 |
| 等待偿债 | Sell，仅限本人拥有的地产 |
| 回合操作 | Upgrade / Sell / EndTurn；升级每回合最多一次 |
| 游戏结束 | 不接受任何玩法命令 |

Upgrade/Sell 必须携带有效地产索引，其他命令不得夹带索引。出售允许不在脚下的本人地产；
购买决定前、掷骰前不能出售；卖掉本回合升级的地产也不恢复升级次数。
真人和电脑的规则权限一致；`IsComputer` 只控制决策器是否提出命令，不是网络身份认证。

调用示意（未来 G3 在页面适配层使用，不把规则复制进 ViewModel）：

```csharp
var session = new RichTownSession(RichTownSnapshot.Create(42UL));
var snapshot = session.Snapshot;
var result = session.Submit(new RichTownRequest(
    snapshot.Revision, snapshot.CurrentPlayerId,
    new RichTownCommand(RichTownCommandKind.Roll)));
// 成功后渲染 result.Snapshot，并按顺序播放 result.Events；动画完成不再次调用 Roll。
```

修订检查解决同一会话的重复点击/并发旧请求；跨重开的旧回调还需要 G4 Document 代数与取消令牌，G2 没有冒充实现该能力。
不同会话没有全局锁；历史快照可在线程间只读持有。32 个同修订并发提交的测试要求恰好一个成功。

## 4. 经济、回合与终局细节

1. Roll 先算骰子和完整路径，再一次结算绕圈奖励和落点。路径包含每一步、目标格；动画不能重复算租金。
2. 现金足额直接付款。不足时记录一笔固定债务，不先扣成负数，也不提前给债权人部分入账。
3. 现金加所有地产半价清算值能付清时停在等待偿债；每次出售后重新判断，足额立即一次支付并回到回合操作。
4. 全部清算仍不足时，在同一命令中按地块编号回收所有本人地产、支付现款、免除余款、淘汰并推进回合；银行不接收超过欠款的金额。
5. 购买价和每级升级成本相同，整块出售值为账面总成本的一半；清空所有者与建筑等级，不实现抵押或拆卖建筑。
6. 座位固定、不新增玩家；取当前座位之后的第一名在场玩家，没有则绕回最小在场座位并进入下一轮。这与“一轮在场者各行动一次”等价，淘汰不会重复行动。
7. 第 30 轮最后一名在场玩家结束或破产时结束整局；仅一人存活则立即结束。总值使用 `long` 整数求和，避免合法 `int` 现金加资产溢出。
8. 30 轮按现金＋购买及升级账面成本降序，同值并列（例：1、1、3）；座位号只稳定显示顺序。出局玩家保留记录但不成为赢家；最后存活者即使资产为零仍获胜。

电脑购买/升级后至少保留 300 现金；升级选择编号最小且可负担的本人地产。
等待偿债时按出售值从低到高、同值按编号出售；每次只提一条命令，读取最新快照后再决策，所以足额后不继续卖房。

## 5. 随机算法与恢复

采用 [Sebastiano Vigna 的 SplitMix64 参考实现](https://prng.di.unimi.it/splitmix64.c)，规则内部标识为 `SplitMix64-v1`。
保留[许可和改编说明](../../../src/ClassicGamePlugin.Plugin/Features/RichTown/Domain/Random-LICENSE.txt)，构建输出为 `Licenses/RichTown/SplitMix64.txt`。
原始源代码声明 public domain 并附许可；本地改编为 C# 显式不可变状态，没有新增二进制随机库。

任意 64 位值可作为初始状态；每次按固定常量递增并混合，模 2^64 的溢出显式写在 `unchecked` 中。
取 `[0, n)` 时丢弃低于 `2^64 mod n` 的样本，再取模，避免余数偏差；骰子取 n=6 后加一，机会取 n=4。
每次重试也推进状态，保存的是最后状态和版本，不能只存种子。未知算法版本或非正上界拒绝，不访问时钟或 `Random.Shared`。

种子 0 的六个固定输出：

```text
e220a8397b1dcdaf 6e789e6aa1b965f4 06c45d188009454f
f88bb8a8724c81ec 1b39896a51a8749b 53cb9f0c747ea2ea
```

测试还从 `61c8864680b583eb` 开始，制造第一次输出零，确认六面骰取样丢弃该样本并保存第二次状态。
这验证了通常极难随机碰到的拒绝分支，不用偶发统计检验冒充等概率证明。

## 6. SOLID 审查

| 原则 | 本轮代码证据 |
| --- | --- |
| S | Board 管配置，Validator 管合法状态，Rules 管转换，Session 管提交所有权，ComputerPlayer 只提决策；规则不混入 View/Document |
| O | 地图/机会值集中为不可变数据，表现读取快照/事件；修改规则版本显式同步配置与测试，不为猜测中的玩法建立策略框架 |
| L | G2 没有可替换服务继承层或随机替身；真实算法和完整规则运行于测试。既有视口契约测试保留 |
| I | 只有简单值对象和窄入口，没有万能 `IGameService`、Service Locator 或额外事件总线 |
| D | 领域只用 BCL 和本子领域，Session 只依赖领域；随机本身无外部效果，以显式状态作为输入，不为纯计算制造接口 |

中文注释说明金额单位、状态边界、线程/快照所有权、失败回滚、偿债一次结算、轮次推进和随机恢复。
结构测试继续禁止跨游戏引用，并新增 Domain/Application 不引用 UI/引擎/SDK/文件/时钟等效果的检查。
源码结构扫描是辅助约束，真正的规则行为由下面的语义测试和完整对局验证。

## 7. 单元测试与完整模拟

| 测试文件 | 核心覆盖 |
| --- | --- |
| [RichTownRandomTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownRandomTests.cs) | 六个参考向量、六面骰各值、低端拒绝重试、范围/版本负例、状态恢复 |
| [RichTownRulesTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownRulesTests.cs) | 开局/地图、路径、跨越或正好落起点、休息/税费/四机会、回合、出局跳过、30 轮和并列 |
| [RichTownEconomyTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownEconomyTests.cs) | 三档价/九档租金及出售、刚好够/不足一元、升级额度、本人/他人地块、多次出售、债权、清算/免除及立即终局 |
| [RichTownValidationTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownValidationTests.cs) | 全阶段×命令矩阵、错误身份/修订/参数、金额/序号溢出、坏快照及不变量 |
| [RichTownSessionAndComputerTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownSessionAndComputerTests.cs) | 32 请求竞争、独立会话、300 现金策略、出售/升级顺序、不接管真人、不窥视随机、购买/债务恢复 |
| [RichTownSimulationTests](../../../tests/ClassicGamePlugin.RichTown.Tests/RichTownSimulationTests.cs) | 种子 0–999 全电脑完整对局，单局上限 2,000 命令，每步现金流水/状态/事件顺序/轮次与恢复一致性 |

完整模拟逐步独立重建资金流水，检查无中间负余额、出局无资产、银行地产无建筑、同轮玩家只掷骰一次、总掷骰不超过 90 次。
每局第 100 条命令前从复制的数组和随机状态构造第二个会话，之后每条命令比较完整快照和具体事件载荷。
失败会给出种子、完整命令前缀和当时快照；成功在 TRX 输出 JSON，开发脚本另外保存 `simulation.json`。
2,000 是保守的硬上限，不是性能指标，也不允许到上限时把未完成对局算通过。

首次完整模拟实测：1,000 局均在 30 轮结束，235,074 条命令，89,703 次掷骰，1,830 次债务，83 次破产；
单局最多 253 条命令，恢复后对比 135,074 条命令。此批种子未触发最后一人终局，该分支由明确场景测试验证。
最终快照集合 SHA-256：`C43151292185F36A8C73428E1811B6C243D2220BDF350B9468A9E02F80029D53`。
摘要用于复现对照，不代替每步语义断言。

## 8. 实际开发验证记录

环境沿用 G1 本地 .NET 10 / Debug。仅使用以下专用入口：

```powershell
pwsh -NoProfile -File scripts/Test-RichTownDevelopment.ps1
```

入口执行锁定还原、Debug 警告即错误构建、全量既有测试与小镇测试、TRX/覆盖率、完整模拟证据、
素材重建摘要、格式、10 份文档本地链接与 Git 差异检查，不部署、不启动窗口。
`-Phase` 仅决定 G1/G2 证据子目录；默认 G2，选择 G1 也不会跳过新规则检查。
八个关键类型 Board/Snapshot/Validator/Random/Rules/Rules.Turn/ComputerPlayer/Session 分别要求行、分支覆盖率至少 95%，
类型/报告缺失、零测试、失败或跳过都视为门禁失败；这只是本地开发门禁。
保留既有魔方 Dispatcher 用例在独立进程执行的做法和全部原始断言，没有根治该旧测试的线程夹具限制。

定向验证记录：首次编译发现局部计算对象主构造捕获警告，已消除冗余捕获；随后警告即错误检查发现两处 xUnit 断言用法问题，已修正。
修正后的定向执行退出 0，126 项通过、0 失败、0 跳过，证据为 `artifacts/rich-town-tests/G2/rules-run/`。
之后补充偿债差一元/任意座位破产与坏快照边界；最终数量以下方全量门禁结果为准，不复用中间数量签署。
首次全量运行 `development-20260918-134213-094` 已通过 555＋1＋130 项测试，但覆盖率收集步骤退出 1：
VSTest 把同一附件复制到运行目录和 TRX 的 In 目录，原脚本错误地要求只有一个物理文件。
现改为检查存在报告且所有副本 SHA-256 完全相同；不同内容仍失败，不合并报告、不挑选较高覆盖率。

**最终全量门禁退出码 0；686 通过、0 失败、0 跳过。** 其中既有回归 555、独立 Dispatcher 用例 1、小镇 130（G1 原有 35，本轮新增 95）。
证据根：`artifacts/rich-town-tests/G2/development-20260918-134409-673/`。
本目录被 Git 忽略，源码与专项记录提交后其他机器需运行入口重新生成，不把本地证据当作已随 Git 分发。

| 检查 | 最终结果 / 文件 |
| --- | --- |
| 锁定还原与 Debug 警告即错误构建 | 退出 0，0 警告、0 错误；`restore.log` / `build.log` |
| 三组全量单测 | 555＋1＋130 通过，无跳过；`existing/results.trx` / `existing-binding/results.trx` / `rich-town/results.trx` |
| 1,000 完整对局与恢复 | `simulation.json`；数量、最高命令数与前述统计和摘要一致 |
| 关键类型覆盖率 | `rules-coverage.json`，所有关键类型均超过门槛 |
| 素材重建 | 7,044 顶点，mesh/RGBA 与锁定 SHA-256 一致；`converted/` |
| 格式、链接、差异 | 格式通过；门禁当时 10 份文档、124 个本地链接通过；暂存/未暂存第一方差异通过 |
| 总结果 | `summary.json`；规则通过，集成明确保持 NOT SIGNED，release 为 not executed |

| 关键类型 | 行覆盖率 | 分支覆盖率 |
| --- | --- | --- |
| RichTownBoard | 100% | 100% |
| RichTownSnapshot | 100% | 100% |
| RichTownSnapshotValidator | 100% | 100% |
| RichTownRandom | 100% | 100% |
| RichTownRules | 97.82% | 97.67% |
| RichTownRules.Turn | 100% | 100% |
| RichTownComputerPlayer | 100% | 100% |
| RichTownSession | 100% | 100% |

唯一未覆盖的规则入口行/分支是命令 switch 的防御性默认分支：前面的 `Enum.IsDefined` 已拒绝非法枚举，六个有效命令都有分支。
保留该防御分支，不移除前置校验、不添加覆盖率排除；金额/债务/随机/终局和非法输入的可达路径均有实际断言。
完成标记与项目/部署说明回填后仅重跑文档链接、行尾空白及差异检查，记录到同目录 `documentation-final-check.json`，无需重复已通过的游戏测试。

覆盖率未覆盖分支需按源码审阅，不能为了达到 100% 移除前置校验或排除规则代码。
G1 的部署和叠层失败未复测，本轮不把它们改成通过。

## 9. 后续路线

1. G2 本地门禁已通过，总记录、主方案和路线图同步完成。
2. 回到 G1 的私有依赖交付冲突、可叠层视口后端及真实 Host/Dock/DPI/资源矩阵。
3. G1/G2 均通过后进入 G3：把已提交快照和事件连接 3D 棋盘、动画与中文操作，不重复结算规则。
4. G4 为同一 Document 增加正式会话所有权、SDK 存档/恢复、Restart 代数与旧回调隔离；G5 完成本地集成收尾。

本轮未使用 AIFLOW、Windows CI、Windows Smoke/Release Acceptance、Release 构建、正式 ZIP、统一 verify/seal 或发布门禁。
未新增托管包/非托管库依赖，不改变 G1 尚未证明自包含的结论；没有发布资格或真实 GPU 通过的暗示。
