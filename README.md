# ClassicGamePlugin

> V6.1 图标同步升级：插件 `1.1.1`，Core/UI SDK `3.4.0`，Build `1.1.3`。
> 图标映射、兼容边界与验证命令见 [专用说明](docs/plan-history/v6.1-plugin-icons.md)。

这是一个提供经典小游戏的 Managed Plugin 解决方案，当前包含彼此独立的扫雷、蜘蛛纸牌、黑白棋、五子棋、围棋、中国象棋、2048、数独、推箱子、俄罗斯方块、空当接龙、消消乐、中国跳棋和三阶魔方 Document。真实交付物是
`src/ClassicGamePlugin.Plugin`；`Standalone` 只负责快速预览 Plugin 中同一份 View、ViewModel、Document 与领域代码。

> 第一次开始开发前，请先阅读 [项目文档与快速开始](docs/README.md)。其中说明了基础项目、小镇专用项目和
> Standalone 窗口的职责、接入真实 Host 的边界，以及临时部署和正式 ZIP 发布流程。

14 个游戏的 23 条“重新开始 / 同步撤销”Workbench Command、多实例路由、SOLID 边界见
[ClassicGame Workbench Command 设计](docs/workbench-commands.md)和
[G8 专用实施记录](docs/plan-history/workbench-command/g8-classic-game-multi-instance-commands.md)。与 Host、
WorkflowStudio 的单轮完整本地封板见
[G10 专项记录](docs/plan-history/workbench-command/g10-classic-game-local-sealing.md)。

扫雷的规则、SOLID 职责划分、设计选择与测试矩阵见
[扫雷 Document 设计与开发说明](docs/minesweeper.md)。

蜘蛛纸牌的三档规则、撤销与提示语义、零图片绘制、交互动画和测试矩阵见
[蜘蛛纸牌 Document 设计与开发说明](docs/spider-solitaire.md)。

黑白棋的现代奥赛罗规则、双人/人机模式、三级电脑、撤销、提示和测试矩阵见
[黑白棋 Document 设计与开发说明](docs/reversi.md)。

五子棋的自由/禁手规则、双人/三级人机、单步回退、AI 搜索和测试矩阵见
[五子棋 Document 设计与开发说明](docs/gomoku.md)。

围棋的标准 19 路、本地双人、提子与全局同形、中国数子、死子标记、轻量动画和测试矩阵见
[围棋 Document 设计与开发说明](docs/go.md)。

中国象棋的标准休闲规则、长将/重复裁定、中文棋谱、决策点撤销、三级 AI 和测试矩阵见
[中国象棋 Document 设计与开发说明](docs/xiangqi.md)。

2048 的经典 4×4 规则、原子移动、方块动画、胜利继续、局部键盘输入和测试矩阵见
[2048 Document 设计与开发说明](docs/2048.md)。

数独的经典 9×9 规则、三级题库、唯一解生成、候选笔记、撤销、提示、计时、动画和测试矩阵见
[数独 Document 设计与开发说明](docs/sudoku.md)。

推箱子的十二张内置地图、文本语法、键盘控制、不限次数撤销、轻量动画和测试矩阵见
[推箱子 Document 设计与开发说明](docs/sokoban.md)。

俄罗斯方块的 7-bag、完整 SRS、暂存与预览、现代计分、确定性 Game Loop、自定义绘制和测试矩阵见
[俄罗斯方块 Document 设计与开发说明](docs/tetris.md)。

空当接龙的标准规则、容量公式、可解编号牌局、求解提示、CardControl 拖放、动画和测试矩阵见
[空当接龙 Document 设计与开发说明](docs/freecell.md)。

消消乐的固定步数挑战、完整特殊组合、连锁消除、提示、拖动交互、轻量动画和测试矩阵见
[消消乐 Document 设计与开发说明](docs/match3.md)。

中国跳棋的 121 孔六角星棋盘、稳定最短连跳、强制撤营、双人/三级人机、路径动画和测试矩阵见
[中国跳棋 Document 设计与开发说明](docs/chinese-checkers.md)。

三阶魔方的 3D 转层、手动操作、按目标分组的层先法还原、完整公式动画、回退与测试矩阵见
[三阶魔方 Document 设计与开发说明](docs/rubiks-cube.md)。本功能仅使用 Debug 开发检查，不调用含 Release 和真实打包的统一 Gate。

富翁小镇 3D（Stride）已实现 **G1 房屋视口原型及 G2 确定性规则；G1 集成仍阻塞**，不计入现有十四个可玩游戏。
Module 目前登记 15 个 Document 和 15 个图标；原有 23 条工作台命令不变。后续逐步开发遵循
[专用设计与开发说明](docs/rich-town.md)及[专项阶段记录](docs/plan-history/rich-town/g0-design-and-development-plan.md)。
已完成内容、Document 状态、后续 G1–G5 工作包与验收条件见[完整开发路线图](docs/rich-town-roadmap.md)。
G2 的规则、偿债/破产、串行会话、电脑策略、随机恢复及 1,000 种子模拟见[G2 专项记录](docs/plan-history/rich-town/g2-deterministic-rules.md)。
按用户本轮要求，纯规则先独立完成；正式 3D 玩法接入仍以解决 G1 为前提，原型页面尚未接入 G2 会话。
SOLID 为首要约束，使用朴素设计、详细中文注释和完整单测；仅执行专用 Debug 本地开发检查，不使用 AIFLOW、Windows CI、
Release 构建、正式 ZIP 打包或发布门禁。下方通用打包命令和统一跨仓 Gate 不适用于该功能当前开发阶段。

小镇开发入口：`pwsh -NoProfile -File scripts/Test-RichTownDevelopment.ps1`（默认 G2 证据目录，含全量单测、1,000 对局及规则覆盖率门禁）。
预览：`dotnet run --project src/ClassicGamePlugin.Standalone -c Debug -p:SkipPluginDeploy=true -- --rich-town-probe`。
该入口和插件登记共用同一页面；默认 Standalone 仍为原有十四个标签页。
当前部署被 `Microsoft.Extensions.DependencyModel.dll` 的共享库禁带规则阻塞，原生视口也不能正确覆盖页面叠层；
见 [G1 结果与下一步](docs/plan-history/rich-town/g1-stride-integration.md)，不要把此分支当作可发布插件。

```powershell
dotnet restore
dotnet build
dotnet run --project src/ClassicGamePlugin.Standalone
dotnet msbuild src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj -t:BuildManagedPluginPackage -p:Configuration=Release
```

要在真实 Host 中调试，请显式提供 Host 的 `Controls` 目录：

```powershell
dotnet msbuild src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj `
  -t:DeployManagedPlugin `
  -p:ManagedPluginDeployRoot=C:\Path\To\Host\Controls
```

Standalone 只能验证十四个游戏的界面和插件自身对象图；manifest、加载上下文、Document Scope、Dock、Tool 和
生命周期必须使用真实 Host 做最终验收。

## 统一跨仓门禁

富翁小镇 3D 的 G0–G5 开发阶段不调用本节入口，具体检查与隔离 Debug Host 联调边界见[专用文档](docs/rich-town.md)。

本仓不再维护 G8/G10 PowerShell 封板入口。请在 `avalonia_dock_simple_test` 主仓根目录运行：

```powershell
dotnet run --project tools/MyAvaloniaManagement.Gate -- verify --scope workbench
dotnet run --project tools/MyAvaloniaManagement.Gate -- seal
```

非默认目录可通过 `--classic-game C:\Path\To\myavalonia-classic-game` 指定。本仓可以带未提交修改，Gate 会
复制 `git ls-files` 描述的实际内容并记录 SHA-256；正式 Host 发布资格仍只绑定干净的主仓提交。历史 G8/G10
文档保留当时的验收事实，但其中脚本命令已经退役。
