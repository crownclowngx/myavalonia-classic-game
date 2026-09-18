# ClassicGamePlugin 开发快速开始

- [V6.1 图标同步升级](plan-history/v6.1-plugin-icons.md)：入口映射、依赖边界和验证结果。
- [富翁小镇完整开发路线图](rich-town-roadmap.md)：本轮做了什么、Document 状态、后续任务顺序及逐阶段验收条件。
- [富翁小镇 3D 专用方案](rich-town.md)：Stride 集成、SOLID 职责、规则与测试矩阵；**G2/G3/G4 已实现，G5 集成核验仍有阻塞**。
- [富翁小镇专项开发记录](plan-history/rich-town/g0-design-and-development-plan.md)：G0–G5 顺序、完成条件、待验证问题与阶段证据。
- [G1 集成结果](plan-history/rich-town/g1-stride-integration.md)：实现边界、实际失败、单测和下一步。
- [G2 确定性规则](plan-history/rich-town/g2-deterministic-rules.md)：纯规则、随机恢复、会话/电脑、债务终局、1,000 种子模拟及开发门禁。
- [G3 可玩 3D](plan-history/rich-town/g3-playable-scene.md)：棋盘、汽车、房屋、镜头、事件回放、中文操作、真实窗口完整对局及 G4 后续边界。
- [G4 SDK 与存档](plan-history/rich-town/g4-document-and-save.md)：持久化协议、保存修订、关闭取消、Restart 与激活租约。
- [G5 本地集成](plan-history/rich-town/g5-local-integration.md)：原生审计、实际窗口循环、只读失败、资源曲线与剩余路线。
- [小镇素材及依赖清单](rich-town-assets-and-dependencies.md)：Kenney 来源、摘要、私有包与自包含限制。

本解决方案用于开发 `myavalonia.plugin.classic.game` Managed Plugin，当前由独立的扫雷、蜘蛛纸牌、黑白棋、五子棋、围棋、中国象棋、2048、数独、推箱子、俄罗斯方块、空当接龙、消消乐、中国跳棋与三阶魔方功能域
分别提供普通 Document。它把真实插件、独立 Avalonia 开发窗口和自动化测试放在同一个解决方案中，使界面与业务代码既能
快速预览，也能由 MyAvaloniaManagement Host 按正式插件协议加载。

## 项目结构

```text
ClassicGamePlugin/
├─ ClassicGamePlugin.slnx
├─ src/
│  ├─ ClassicGamePlugin.Plugin/          # 唯一插件入口；各游戏独立子领域
│  ├─ ClassicGamePlugin.RichTown.Stride/ # 小镇专用渲染与资源，不依赖其他游戏
│  └─ ClassicGamePlugin.Standalone/      # 只供本地开发的 Avalonia 窗口
├─ tests/
│  ├─ ClassicGamePlugin.Tests/           # 既有游戏及插件注册契约
│  └─ ClassicGamePlugin.RichTown.Tests/  # 小镇独立测试
└─ docs/                       # 当前项目随模板生成的开发说明
```

`ClassicGamePlugin.Plugin` 是唯一正式插件项目。Standalone 和 Tests 都直接引用它，不能各自复制一套 View、
ViewModel、服务或贡献清单。

## 最短开发流程

在解决方案根目录打开 PowerShell：

```powershell
dotnet restore
dotnet build -c Debug -warnaserror
dotnet test -c Debug --no-build
dotnet run --project src/ClassicGamePlugin.Standalone
```

Standalone 适合快速检查 AXAML、编译绑定、命令和插件自身对象图。写到可以联调时，再把干净的插件目录
部署到真实 Host；发布前则必须生成正式 ZIP。不要把 Standalone 能运行当成 Host 验收已经通过。

富翁小镇当前只执行[专用方案](rich-town.md)中的 Debug 开发检查；普通构建显式设置 `SkipPluginDeploy=true`。
G1 后仅在显式步骤中生成 Debug 暂存目录并使用隔离开发 Host，不覆盖日常 Host 和数据，不运行 Release/正式 ZIP、
统一 `verify` / `seal`、Windows CI 或发布门禁，不使用 AIFLOW。默认开发脚本不启动窗口，G3 使用显式 `Test-RichTownPlayableWindow.ps1` 做本机 GPU 完整对局检查；不部署、不启动 Host。

## 接下来阅读

1. [扫雷 Document 设计与开发说明](minesweeper.md)
2. [蜘蛛纸牌 Document 设计与开发说明](spider-solitaire.md)
3. [黑白棋 Document 设计与开发说明](reversi.md)
4. [五子棋 Document 设计与开发说明](gomoku.md)
5. [围棋 Document 设计与开发说明](go.md)
6. [中国象棋 Document 设计与开发说明](xiangqi.md)
7. [2048 Document 设计与开发说明](2048.md)
8. [数独 Document 设计与开发说明](sudoku.md)
9. [推箱子 Document 设计与开发说明](sokoban.md)
10. [俄罗斯方块 Document 设计与开发说明](tetris.md)
11. [空当接龙 Document 设计与开发说明](freecell.md)
12. [消消乐 Document 设计与开发说明](match3.md)
13. [中国跳棋 Document 设计与开发说明](chinese-checkers.md)
14. [三阶魔方 Document 设计与开发说明](rubiks-cube.md)
15. [项目、Host 与 Standalone 窗口职责](project-and-window-responsibilities.md)
16. [临时部署、正式发布与验收](deployment-and-release.md)
17. [Workflow Action Provider 与 Consumer 接入](workflow-actions.md)
18. [ClassicGame Workbench Command 设计](workbench-commands.md)
19. [Workbench Command G8 专用实施记录](plan-history/workbench-command/g8-classic-game-multi-instance-commands.md)
20. [Workbench Command G10 本地封板记录](plan-history/workbench-command/g10-classic-game-local-sealing.md)
21. [富翁小镇 3D：Stride Document 设计与分阶段开发说明](rich-town.md)
22. [富翁小镇 3D 专项开发记录](plan-history/rich-town/g0-design-and-development-plan.md)
23. [富翁小镇完整开发路线图](rich-town-roadmap.md)

## 开发前记住

- `myavalonia.plugin.classic.game` 是持久身份，发布后不要因为显示名、项目名或文件夹改名而改变它。
- manifest 由 Build 包生成，不要手写或复制一份长期维护。
- 插件只通过公开 Plugin SDK 接入 Host，不引用 Host 内部项目。
- 新增插件运行时 NuGet 包时，要同时更新根目录 `Directory.Packages.props`、Plugin 项目的
  `PackageReference` 和 `ManagedPluginPrivatePackage`；完整示例见部署文档。
- 当前交付目标是 Windows x64；插件替换后必须完整重启 Host，不支持热更新。
- 当前游戏开发阶段只要求 Debug 警告即错误和全量单元测试通过，不运行 Windows CI、Release 打包或发布门禁。
- 三阶魔方本次实施按其专用文档执行 Debug 单测、覆盖率与静态检查，不调用统一 `verify`/`seal`，不使用 AIFLOW。
- 富翁小镇按专用文档逐阶段执行 SOLID 审查、完整单测和本地检查，同阶段同步文档与证据；未执行项目不得记作通过。
- G8/G10 历史脚本已退役，历史文档保留当时事实；统一跨仓入口见根 README。富翁小镇当前阶段不调用其中任何入口，
  即使涉及工作台命令或依赖调整，也只执行其专用 Debug 开发检查。
- Workflow Action Provider 与 Consumer 是两种互斥角色，选择前先阅读专项文档，不要在同一插件中同时注册。
