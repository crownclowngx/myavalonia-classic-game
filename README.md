# ClassicGamePlugin

> 当前开发版本：插件 `1.4.0`，Core/UI SDK `3.4.1`，Avalonia `12.1.2`，Build `3.4.2-richtown.1`（保留小镇部署修复）。
> 扫雷提供两个独立且默认关闭的选项：“自动插旗”和“插旗后继续探索”。后者按现有旗帜自动展开数字格邻域，再继续推理；错旗仍可能踩雷。
> 图标映射、兼容边界与验证命令见 [专用说明](docs/plan-history/v6.1-plugin-icons.md)。

这是一个提供经典小游戏的 Managed Plugin 解决方案，当前包含彼此独立的扫雷、蜘蛛纸牌、黑白棋、五子棋、围棋、中国象棋、2048、数独、推箱子、俄罗斯方块、空当接龙、消消乐、中国跳棋和三阶魔方 Document。真实交付物是
`src/ClassicGamePlugin.Plugin`；`Standalone` 只负责快速预览 Plugin 中同一份 View、ViewModel、Document 与领域代码。

> 第一次开始开发前，请先阅读 [项目文档与快速开始](docs/README.md)。其中说明了基础项目、小镇专用项目和
> Standalone 窗口的职责、接入真实 Host 的边界，以及临时部署和正式 ZIP 发布流程。

现有十四游戏的命令加上小镇 Restart，现为 24 条 Workbench Command；路由和 SOLID 边界见
[ClassicGame Workbench Command 设计](docs/workbench-commands.md)和
[G8 专用实施记录](docs/plan-history/workbench-command/g8-classic-game-multi-instance-commands.md)。与 Host、
WorkflowStudio 的单轮完整本地封板见
[G10 专项记录](docs/plan-history/workbench-command/g10-classic-game-local-sealing.md)。

扫雷的规则、SOLID 职责划分、设计选择与测试矩阵见
[扫雷 Document 设计与开发说明](docs/minesweeper.md)。升级与验证记录见
[1.3.0 扫雷自动插旗](docs/plan-history/minesweeper-auto-flag.md)。
独立探索选项及交替推理见 [1.4.0 插旗后继续探索](docs/plan-history/minesweeper-auto-explore.md)。

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

富翁小镇 3D（Stride）已实现 **G2 规则、G3 可玩和 G4 SDK 存档；G5 已开展核验，尚有集成阻塞**，与现有十四个游戏保持子领域独立。
Module 目前登记 14 个普通 Document + 1 个持久化小镇、15 个图标、24 条工作台命令。后续开发遵循
[专用设计与开发说明](docs/rich-town.md)及[专项阶段记录](docs/plan-history/rich-town/g0-design-and-development-plan.md)。
已完成内容、Document 状态、后续 G1–G5 工作包与验收条件见[完整开发路线图](docs/rich-town-roadmap.md)。
G2 的规则、偿债/破产、串行会话、电脑策略、随机恢复及 1,000 种子模拟见[G2 专项记录](docs/plan-history/rich-town/g2-deterministic-rules.md)。
按用户本轮要求，G3 已接入同一 Document：24 格、汽车、建筑、镜头、动画和中文操作，一真人两电脑可完成整局，见[G3 专项记录](docs/plan-history/rich-town/g3-playable-scene.md)。
SDK schema 1、保存修订、工作台 Restart、关闭取消和单会话激活见[G4 专项记录](docs/plan-history/rich-town/g4-document-and-save.md)。
依赖审计与历史限制见[G5 专项记录](docs/plan-history/rich-town/g5-local-integration.md)；部署修复、真实 Host 棋盘与回合实测见[1.2.0 发布记录](docs/plan-history/rich-town/g5-deployment-repair.md)。
SOLID 为首要约束，使用朴素设计、详细中文注释和完整单测；日常使用专用 Debug 本地开发检查。
本次按用户明确授权执行 1.2.0 本地 Release 测试、标准 ZIP 和桌面部署；不使用 AIFLOW、Windows CI 或统一跨仓 verify/seal。

小镇开发入口：`pwsh -NoProfile -File scripts/Test-RichTownDevelopment.ps1`（默认 G5 证据目录，含全量单测、1,000 对局、规则/展示/存档覆盖率及依赖审计）。
可玩预览：`dotnet run --project src/ClassicGamePlugin.Standalone -c Debug -p:SkipPluginDeploy=true -- --rich-town-play`。
该入口和插件登记共用同一游戏页面；`--rich-town-probe` 保留独立 G1 诊断页面；默认 Standalone 仍为原有十四个标签页。
显式本机完整窗口检查：`pwsh -NoProfile -File scripts/Test-RichTownPlayableWindow.ps1`，需要本机桌面与 GPU，不属于 Windows CI。
DependencyModel 禁带冲突、资源映射和 Host 中 SDL 查找已修复；原生视口叠层仍待完善；
此外 Stride 在只读程序目录初始化失败，20 次循环后句柄增长仍待定位；见 [G5 结果与下一步](docs/plan-history/rich-town/g5-local-integration.md)。

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

Standalone 只能验证游戏界面和插件自身对象图；插件 manifest、加载上下文、Document Scope、Dock、Tool 和
生命周期必须使用真实 Host 做最终验收。

## 插件验证与 Host 联调

在本仓运行独立验证，包含游戏规则、小镇和 UI 三个测试项目：

```powershell
dotnet restore --locked-mode -p:SkipPluginDeploy=true
dotnet build -c Debug --no-restore -warnaserror -p:SkipPluginDeploy=true
dotnet test -c Debug --no-build --no-restore
```

主仓 Gate 的 `workbench` / `workflow` scope 与 `--classic-game` 参数已经退役，外部插件业务验证由本仓负责。
Host 联调使用主仓显式启用的 `HostExternalPluginAcceptance` 测试变体，输入隔离暂存目录，验证真实 DLL 加载、
共享依赖、视图创建和 Dock 生命周期。扫雷辅助功能的最新验证见[1.4.0 升级记录](docs/plan-history/minesweeper-auto-explore.md)。
历史 G8/G10 记录仍保留原有验收事实。富翁小镇阶段边界见[专用文档](docs/rich-town.md)。
