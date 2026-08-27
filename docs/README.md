# ClassicGamePlugin 开发快速开始

本解决方案用于开发 `myavalonia.plugin.classic.game` Managed Plugin，当前由独立的扫雷、蜘蛛纸牌、黑白棋、五子棋、中国象棋、2048 与数独功能域
分别提供普通 Document。它把真实插件、独立 Avalonia 开发窗口和自动化测试放在同一个解决方案中，使界面与业务代码既能
快速预览，也能由 MyAvaloniaManagement Host 按正式插件协议加载。

## 项目结构

```text
ClassicGamePlugin/
├─ ClassicGamePlugin.slnx
├─ src/
│  ├─ ClassicGamePlugin.Plugin/       # 唯一真实插件程序集和正式交付内容
│  └─ ClassicGamePlugin.Standalone/   # 只供本地开发的 Avalonia 窗口
├─ tests/
│  └─ ClassicGamePlugin.Tests/        # 插件业务、状态和注册行为测试
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

## 接下来阅读

1. [扫雷 Document 设计与开发说明](minesweeper.md)
2. [蜘蛛纸牌 Document 设计与开发说明](spider-solitaire.md)
3. [黑白棋 Document 设计与开发说明](reversi.md)
4. [五子棋 Document 设计与开发说明](gomoku.md)
5. [中国象棋 Document 设计与开发说明](xiangqi.md)
6. [2048 Document 设计与开发说明](2048.md)
7. [数独 Document 设计与开发说明](sudoku.md)
8. [项目、Host 与 Standalone 窗口职责](project-and-window-responsibilities.md)
9. [临时部署、正式发布与验收](deployment-and-release.md)
10. [Workflow Action Provider 与 Consumer 接入](workflow-actions.md)

## 开发前记住

- `myavalonia.plugin.classic.game` 是持久身份，发布后不要因为显示名、项目名或文件夹改名而改变它。
- manifest 由 Build 包生成，不要手写或复制一份长期维护。
- 插件只通过公开 Plugin SDK 接入 Host，不引用 Host 内部项目。
- 新增插件运行时 NuGet 包时，要同时更新根目录 `Directory.Packages.props`、Plugin 项目的
  `PackageReference` 和 `ManagedPluginPrivatePackage`；完整示例见部署文档。
- 当前交付目标是 Windows x64；插件替换后必须完整重启 Host，不支持热更新。
- 当前游戏开发阶段只要求 Debug 警告即错误和全量单元测试通过，不运行 Windows CI、Release 打包或发布门禁。
- Workflow Action Provider 与 Consumer 是两种互斥角色，选择前先阅读专项文档，不要在同一插件中同时注册。
