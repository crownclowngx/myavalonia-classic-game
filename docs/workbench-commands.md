# ClassicGame Workbench Command 设计

> 当前实现：Workbench Command G8，ClassicGame `1.1.0`，Core/UI SDK `3.3.0`。

## 1. 全游戏命令矩阵

| 游戏 | Restart | Undo | Tools 菜单 | G8 工作台快捷键 |
| --- | --- | --- | --- | --- |
| 扫雷 | 是 | 无此业务能力 | Hide | — |
| 蜘蛛纸牌 | 是，重开同一牌局 | 是 | Hide | — |
| 黑白棋 | 是 | 是 | Hide | — |
| 五子棋 | 是 | 是 | Hide | `Ctrl+Shift+R` / `Ctrl+Z` |
| 围棋 | 是 | 是 | Hide | — |
| 中国象棋 | 是 | 是 | Hide | — |
| 2048 | 是 | 无此业务能力 | Hide | — |
| 数独 | 是，重开当前题目 | 是 | Hide | — |
| 推箱子 | 是，重开当前关卡 | 是 | Hide | — |
| 俄罗斯方块 | 是 | 无此业务能力 | Hide | — |
| 空当接龙 | 是，重开同一牌局 | 是 | Hide | — |
| 消消乐 | 是 | 无此业务能力 | Hide | — |
| 中国跳棋 | 是 | 是 | Hide | — |

命名统一为 `myavalonia.plugin.classic.game.command.<game>.restart|undo`，菜单 Placement 统一为
`myavalonia.plugin.classic.game.command-placement.menu.tools.<game>.restart|undo`。共注册 22 条 Command 和
22 条 Tools 菜单：13 条 Restart、9 条 Undo。未支持撤销的四个游戏不注册占位命令。

Host 3.3 冻结契约要求一条 CommandId 只绑定一个 DocumentTypeId，并拒绝同一插件内重复 Gesture。G8 因此
保留五子棋作为 `Ctrl+Shift+R` / `Ctrl+Z` 的快捷键端到端样本；其余游戏通过 Catalog/Menu 接入，后续 G9
可以直接投影到 Command Palette。这里不修改 SDK 或 Host 内核，也不注册会被 Host 禁用的冲突快捷键。

五子棋快捷键样本的两个完整稳定身份为：

- `myavalonia.plugin.classic.game.command.gomoku.restart`
- `myavalonia.plugin.classic.game.command.gomoku.undo`

## 2. 唯一执行链与实例所有权

```text
Host MenuItem / Gomoku KeyBinding
              │ CommandId
              ▼
Catalog → Context → Executor（执行前重查）
              │ 当前活动 Document Scope
              ▼
13 个 Game Document : IWorkbenchDocumentCommandTarget
              │
WorkbenchDocumentCommandAdapter
      ├─ Restart → 既有 Restart / ReplaySameDeal Command
      └─ Undo    → 既有 UndoCommand（仅 9 个游戏）
```

每个 Document 只选择本实例已有的本地命令；领域规则、历史、AI、计时、动画和页面文案仍归原 ViewModel/领域。
View 内按钮与工作台入口调用同一个命令对象，不产生第二套用例。内部
`WorkbenchDocumentCommandAdapter` 只集中处理身份分派、执行前防御、同步取消边界和事件订阅；事件 sender
仍保持为 Document Target，以满足 Host 对迟到通知的引用校验。

状态通知始终携带一个准确 CommandId。Dispose 先使所有命令 fail closed 并成对退订，再由有资源的 Document
级联释放 ViewModel。同步 RelayCommand 返回已完成 `ValueTask`，不使用 `async void`、`Task.Run` 或未观察任务。

## 3. SOLID 与朴素模式

| 原则 | 落地方式 |
| --- | --- |
| SRP | `PluginIds` 只管身份，Module 只声明 Descriptor，Document 选择用例，内部 Adapter 只管协议分派 |
| OCP | 13 个 Document 通过同一公开 Target 契约扩展，Host Executor、SDK 与 schema 均不修改 |
| LSP | 所有 Target 对未知、禁用、预取消和释放状态遵守相同失败语义 |
| ISP | Target 只暴露查询、可等待执行和定向事件，不取得 Provider、Dock、Control 或 Registry |
| DIP | ClassicGame 精确依赖公开 NuGet；Host 只消费真实 ZIP，双方没有源码 ProjectReference |

使用的模式只有稳定身份、不可变 Descriptor 和组合式窄 Adapter。没有 Mediator、事件总线、CQRS、服务定位器、
字符串 `when` 表达式或反射命令发现。Host 真实包测试中的反射只用于驱动外部 ALC 内的五子棋状态，不进入生产代码。

## 4. 测试与本地非发布门禁

```powershell
pwsh -NoProfile -File .\scripts\Test-ClassicGameWorkbenchCommandG8.ps1 -Configuration Release
```

入口使用纯 NuGet.org 和隔离缓存完成 locked restore、零警告构建、格式验证、全量单测、覆盖率、Standalone
构建、两轮确定性 ZIP、manifest/共享 SDK 边界和 Markdown 链接检查。Host 侧通过生产 Loader、独立 ALC、
13 个真实 Document Scope、五子棋双实例、Host-owned 菜单/快捷键和窗口释放复核整条链。

```text
aiflow=false
windowsCi=false
windowsSmoke=false
releaseAcceptance=false
releaseGate=false
publishable=false
published=false
uploaded=false
signed=false
tagCreated=false
```

Release 仅是本地编译配置。完整实数与回滚边界见
[G8 专用实施记录](plan-history/workbench-command/g8-classic-game-multi-instance-commands.md)。
