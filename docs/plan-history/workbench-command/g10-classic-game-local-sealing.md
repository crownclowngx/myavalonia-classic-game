# Workbench Command G10：ClassicGame 本地封板

> 状态：已完成（2026-08-29；跨仓单轮完整本地非发布门禁）。
>
> 输入提交：`6033030008f11c9cf9491c383532d21fc9fe594e`
>
> 前置：[G8 全游戏多实例命令](./g8-classic-game-multi-instance-commands.md)

## 1. 结论与设计

G10 没有修改十三个游戏的生产规则、ViewModel、public API 或局部 RelayCommand。插件仍为 1.1.0，精确消费
Core/UI 3.3.0，manifest schema 2 与 SDK 区间 `[3.3.0,4.0.0)` 不变；13 条 Restart、9 条已有 Undo、
22 条 Tools 菜单和五子棋两个快捷键保持 G8 语义。

SOLID 仍是首要纪律：G8 叶子脚本拥有十三游戏、覆盖率、确定性 ZIP 和 manifest 规则；
`Test-ClassicGameWorkbenchCommandG10.ps1` 只验证 G8 摘要并投影稳定事实。各 Document 继续显式选择窄
Target/Adapter，状态属于当前游戏实例，没有插件单例命令状态、反射发现或通用命令总线。

## 2. 本仓与 Host 验收

```powershell
pwsh -NoProfile -File .\scripts\Test-ClassicGameWorkbenchCommandG10.ps1 -Configuration Release
```

复用的 G8 实测为 **526/526**，失败 0、跳过 0；总行/分支覆盖率 **71.75% / 58.36%**，
`GomokuDocument` 与 `WorkbenchDocumentCommandAdapter` 行覆盖率均为 **100%**。两次确定性 ZIP 均为
4 个文件；最终跨物理根 SHA-256 由 Host G10 的 `artifacts/test-results/WorkbenchCommandG10/summary.json`
记录，本文不预填依赖旧构建根的哈希，避免门禁完成后回写本文而使已签署的工作树指纹失效。

Host G10 另把本包与 WorkflowStudio 实体包同时加载，验证 25 条外部命令共同注册；Headless MainWindow
反复切换 Studio、五子棋 A/B 和无活动 Document，确认 Restart/Undo、菜单、快捷键和 Palette 只指向当前
实例，关闭后没有旧 Target 或重复订阅。完整跨仓证据由 Host 的 G10 `summary.json` 保存。

## 3. 非发布与回滚

本轮不使用 AIFLOW，不运行 Windows CI、Windows Smoke、Release Acceptance 或发布门禁，不上传、不签名、
不打 tag，Release 仅是编译配置。回滚只移除 G10 包装脚本、记录和索引；G8 的 22 条命令和原游戏 UI 保持。

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
