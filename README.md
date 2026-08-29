# ClassicGamePlugin

这是一个提供经典小游戏的 Managed Plugin 解决方案，当前包含彼此独立的扫雷、蜘蛛纸牌、黑白棋、五子棋、围棋、中国象棋、2048、数独、推箱子、俄罗斯方块、空当接龙、消消乐和中国跳棋 Document。真实交付物是
`src/ClassicGamePlugin.Plugin`；`Standalone` 只负责快速预览 Plugin 中同一份 View、ViewModel、Document 与领域代码。

> 第一次开始开发前，请先阅读 [项目文档与快速开始](docs/README.md)。其中说明了三个子项目和
> Standalone 窗口的职责、接入真实 Host 的边界，以及临时部署和正式 ZIP 发布流程。

13 个游戏的 22 条“重新开始 / 已有撤销”Workbench Command、多实例路由、SOLID 边界与本地非发布门禁见
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

Standalone 只能验证十三个游戏的界面和插件自身对象图；manifest、加载上下文、Document Scope、Dock、Tool 和
生命周期必须使用真实 Host 做最终验收。
