# Workbench Command G8：ClassicGame 全游戏多实例命令

> 状态：已完成（2026-08-28；双仓本地非发布门禁通过）。
>
> ClassicGame 输入提交：`1d11f1689433caf242365480233dd76ff5c8836b`
>
> ClassicGame 输入 Git tree：`1f3e826735c6ea191bbf68cb658557260a734fc2`
>
> 设计说明：[ClassicGame Workbench Command](../../workbench-commands.md)

## 1. 基线与边界

G8 修改前冻结到插件 `1.0.0`、Core/UI SDK `3.2.0`、13 个普通 Document 和 409 个测试声明。工作树显示的
304 项变化均为 LF 到 CRLF 的行尾转换，`git diff --ignore-space-at-eol --quiet` 证明没有语义差异；实施没有
reset、清理或全仓格式化这些用户状态。

输入 locked restore 因公开源中 SDK 3.2.0 与历史 lock file 的内容哈希不一致而以 `NU1403` 失败，此事实原样
记录，不伪造基线通过。G8 升级到 Core/UI `3.3.0`，插件提升为 `1.1.0`，manifest 最低 SDK 为 `3.3.0`；
schema 2 和最大版本 `4.0.0` 均不变。

## 2. 最终实现与设计思路

```text
ClassicGamePluginModule
  ├─ 13 × Restart Descriptor / Tools Menu
  ├─  9 × Undo Descriptor / Tools Menu
  └─ Gomoku：Ctrl+Shift+R / Ctrl+Z
                         │
                         ▼ Host 当前实例路由
13 个 Document Target → WorkbenchDocumentCommandAdapter
                         ├─ 身份与执行前防御
                         ├─ 定向状态事件
                         └─ 既有 RelayCommand
```

用户追加要求覆盖其他游戏后，范围扩展为 13 条 Restart 和 9 条已有 Undo，共 22 条命令。蜘蛛纸牌与空当接龙
的 Restart 复用 `ReplaySameDealCommand`；数独和推箱子的 Undo RelayCommand 补齐既有 `CanUndo` 判定。2048、
扫雷、消消乐、俄罗斯方块没有撤销业务能力，未注册伪造命令。

Host 3.3 会拒绝同一插件重复 Gesture，且每条 Command 只能绑定一个 DocumentTypeId。因此没有为 13 个游戏
重复声明 `Ctrl+Shift+R` / `Ctrl+Z`，也没有修改冻结 SDK/Host 内核；五子棋保留为快捷键样本，其他命令已进入
Catalog/Menu，并可在 G9 被 Palette 投影。

生产实现只使用稳定身份、不可变 Descriptor 和一个内部组合式窄 Adapter。Adapter 不懂游戏规则、不拥有
ViewModel，也不发现命令；每个 Document 显式选择既有命令，并保证事件 sender 仍是 Target 本身。Dispose
先退订/fail closed，再释放 ViewModel，避免迟到通知和实例串线。

## 3. 测试与跨仓库验收

ClassicGame 单测覆盖 22 个 CommandId、24 个 PlacementId、13 个 Document、全游戏 Restart、9 个 Undo 初态、
未知 ID、禁用执行、预取消、定向通知、重复 Dispose 和释放后 fail closed。五子棋继续覆盖 A/B 双实例落子、
Undo/Restart 只影响活动实例。

Host 专项通过真实 ZIP、生产 Loader、独立 ALC、Provider、Registry 和 Document Scope 构造 13 个真实游戏，
逐一执行 Restart 并核对 Undo 能力；Headless MainWindow 逐个切换 13 个活动目标，验证 Tools 菜单 Hide/Enabled，
再用两个真实五子棋实例复核快捷键路由与窗口关闭清理。

最终实测结果：ClassicGame **526/526**，失败 0、跳过 0；总行/分支覆盖率
**71.75% / 58.36%**，高于冻结基线 **70.87% / 57.82%**；`GomokuDocument` 与
`WorkbenchDocumentCommandAdapter` 行覆盖率均为 **100%**。两轮确定性 ZIP 均为 4 个文件，SHA-256 为
`4A1C7358BEEC84361C123E1B60ABEE2F372190DAD930FE0FE10F4CFE31F77EB9`。

Host 基础门禁为 **575/575**，行/分支覆盖率 **86.98% / 72.42%**；真实包 PluginTests **1/1**，
Headless UI **1/1**。机器摘要位于：

```text
artifacts/test-results/ClassicGameWorkbenchCommandG8/summary.json
artifacts/test-results/WorkbenchCommandG8/summary.json
```

## 4. 门禁与回滚

```powershell
# ClassicGame 独立公开源、覆盖率和确定性包
pwsh -NoProfile -File .\scripts\Test-ClassicGameWorkbenchCommandG8.ps1 -Configuration Release

# Host 基础开发门禁、真实包和 Headless UI
pwsh -NoProfile -File .\scripts\Test-WorkbenchCommandG8.ps1 -Configuration Release
```

G8 不使用 AIFLOW，不调用 Windows CI、Windows Smoke、Release Acceptance 或发布门禁，不上传、不签名、
不打 tag，也不形成发布资格。回滚时移除 22 个 Command、22 个菜单、2 个快捷键 Placement、13 个 Target、
内部 Adapter、专项测试/脚本/文档，并恢复 `1.0.0`、SDK `3.2.0` 与 lock file；原游戏 View 命令必须保留。

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
