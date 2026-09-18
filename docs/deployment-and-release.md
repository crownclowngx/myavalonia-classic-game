# 临时部署、正式发布与验收

> 当前跨仓验收统一由主仓 `tools/MyAvaloniaManagement.Gate` 执行。本文中的 `scripts/*.ps1` 命令是历史记录，
> 已退役；使用方式见本仓 README。

> **富翁小镇 3D 当前处于开发阶段。** 依照[专用方案](rich-town.md)和[专项记录](plan-history/rich-town/g0-design-and-development-plan.md)，
> 不执行本文的 Release、正式 ZIP、历史 G8/G10 或统一 Gate/发布步骤，不使用 AIFLOW、Windows CI。
> G1 起仅显式生成 Debug 暂存目录并在隔离开发 Host 验证私有依赖，不覆盖日常插件和用户数据；普通构建设置 `SkipPluginDeploy=true`。
> Stride 完整运行时文件、原生二级依赖、内容数据库和 VC++ 运行库目前均待验证，不宣称已实现自包含。
> G1 已声明 42 个私有运行依赖包和小镇资源，Debug 部署实测被 Build 1.1.3 的
> `Microsoft.Extensions.DependencyModel.dll` 禁带规则阻止；尚未产生通过协议的暂存目录。
> [G1 记录](plan-history/rich-town/g1-stride-integration.md)保留失败命令；[依赖说明](rich-town-assets-and-dependencies.md)列出文件与后续条件。
> [G2 规则阶段](plan-history/rich-town/g2-deterministic-rules.md)已通过无窗口 Debug 开发门禁，未执行部署；未新增 NuGet 或非托管库。
> 仅在现有小镇许可证路径增加 `SplitMix64.txt`，不改变 G1 的自包含待验证结论。
> [G3 可玩阶段](plan-history/rich-town/g3-playable-scene.md)接通同一 Document 和真实 Standalone 完整对局，没有新增包/非托管依赖或执行部署。
> G4 已接通 SDK 持久化与 Restart；G5 重新尝试隔离 Debug 部署，仍失败于同一 DependencyModel 禁带冲突。
> G5 原生导入审计、中文路径窗口循环、只读目录失败及句柄增长见 [专用记录](plan-history/rich-town/g5-local-integration.md)。
> 本轮没有合规插件目录，Standalone 测试副本不是部署产物，不执行下文发布步骤。
> 新增窗口检查脚本只运行本机 Debug，不是 Windows CI 或发布门禁；Windows manifest 仅属于开发程序，不修改 Host。

部署分为开发期临时联调和正式 ZIP 发布。两者都必须使用 Build 包筛选出的干净插件目录，不能直接复制
普通 `bin/Debug` 或 `bin/Release`，因为普通输出可能包含 Host 应当统一提供的共享程序集。

## 新增 NuGet 包后必须同步的三个位置

插件业务代码新增运行时 NuGet 依赖时，以 `Some.Private.Runtime` 为例，同时修改以下位置：

```xml
<!-- 1. 解决方案根 Directory.Packages.props：统一锁定版本 -->
<ItemGroup>
  <PackageVersion Include="Some.Private.Runtime" Version="[1.2.3]" />
</ItemGroup>

<!-- 2、3. src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj：引用并声明由插件携带 -->
<ItemGroup>
  <PackageReference Include="Some.Private.Runtime" />
</ItemGroup>
<ItemGroup>
  <ManagedPluginPrivatePackage Include="Some.Private.Runtime" />
</ItemGroup>
```

`ManagedPluginPrivatePackage` 是正式交付资产所有权声明，不是重复的 `PackageReference`。Build 只从这里列出的
NuGet 包收集托管 DLL 和当前 `win-x64` RID 资产；漏写时 Standalone 或普通 `bin` 可能正常，正式 ZIP 却会
缺少 DLL，真实 Host 最终报 `FileNotFoundException`、`FileLoadException` 或类型初始化失败。

还要注意：

- `Include` 使用准确的 NuGet 包 ID；若直接包依赖其他提供运行时文件的包，也要逐一列出这些传递包 ID；
- 用 `dotnet list src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj package --include-transitive` 查看传递依赖；
- SDK、Avalonia、Dock、Semi、Ursa、CommunityToolkit、`Microsoft.Extensions.*` 和 Newtonsoft.Json 是当前
  Host 共享边界，不添加到 `ManagedPluginPrivatePackage`，也不应出现在 ZIP；
- 只给 Standalone 或 Tests 使用的包只添加到对应项目，不添加到 Plugin 项目；
- NuGet 包若通过构建 Target 生成完整原生目录，使用 `ManagedPluginAssetDirectoryRelativePath`；其他额外
  文件使用带 `TargetPath` 的 `ManagedPluginAsset`。

发布前用 `-p:ManagedPluginTraceAssets=true` 输出最终资产映射，并解压 ZIP 确认插件私有 DLL 与原生文件都在
`Controls/ClassicGamePlugin/` 内。

## 临时部署到真实 Host

部署或替换前先完整退出 Host。当前插件发现和加载上下文以进程为边界，不支持热替换。

### 方式一：直接部署

已知 Host 的 `Controls` 目录时，在解决方案根目录执行：

```powershell
dotnet msbuild src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj `
  -t:DeployManagedPlugin `
  -p:Configuration=Debug `
  -p:ManagedPluginDeployRoot=C:\Path\To\Host\Controls
```

该目标只重建 `Controls/ClassicGamePlugin`，不会清理 `Controls` 根目录或其他插件。需要给开发目录增加醒目标记时，
可覆盖目录名：

```powershell
dotnet msbuild src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj `
  -t:DeployManagedPlugin `
  -p:Configuration=Debug `
  -p:ManagedPluginDeployRoot=C:\Path\To\Host\Controls `
  -p:ManagedPluginDirectoryName=ClassicGamePlugin-Dev
```

`ClassicGamePlugin-Dev` 只是文件夹名称，插件身份仍是 manifest 中的 `myavalonia.plugin.classic.game`。

### 方式二：生成暂存目录后手工复制和改名

需要先检查产物或用资源管理器复制时，先部署到一个独立暂存根：

```powershell
dotnet msbuild src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj `
  -t:DeployManagedPlugin `
  -p:Configuration=Debug `
  -p:ManagedPluginDeployRoot=C:\Temp\ClassicGamePlugin-Deploy\Controls
```

然后把整个 `C:\Temp\ClassicGamePlugin-Deploy\Controls\ClassicGamePlugin` 复制到 Host 的 `Controls` 下。目标叶子目录可以
改为 `ClassicGamePlugin-Dev`，但必须遵守：

- 把插件目录作为一个整体替换，不要把新文件合并覆盖到旧目录，否则删除过的依赖可能残留；
- 同一个 Host 中只保留一份 `myavalonia.plugin.classic.game`，不能同时留下 `ClassicGamePlugin` 和 `ClassicGamePlugin-Dev`；
- 不修改 `plugin.manifest.json`，不因为临时目录改名而改变 Plugin、Document 或 Tool ID；
- 复制完成后重新启动 Host，再从插件状态和真实 Dock 验证加载结果。

## 正式发布 ZIP

发布前先完成 Release 构建和测试，并按兼容变更更新 `PluginVersion`：

```powershell
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet msbuild src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj `
  -t:BuildManagedPluginPackage `
  -p:Configuration=Release
```

默认输出：

```text
src/ClassicGamePlugin.Plugin/artifacts/managed-plugin-packages/
├─ ClassicGamePlugin.Plugin-1.0.0-win-x64.zip
└─ ClassicGamePlugin.Plugin-1.0.0-win-x64.manifest.json
```

ZIP 内保持 `Controls/ClassicGamePlugin/` 布局；同名外置 `.manifest.json` 记录 ZIP 和文件摘要。正式交付时让二者
保持配对，不要手工重压 ZIP、编辑 ZIP 内 manifest，或把 `bin` 目录自行压缩成发布包。安装时优先使用
Host 提供的导入入口；若由维护者手工解压，也必须保留 ZIP 内的目录层级。

## 真实 Host 最小验收

- 插件状态显示已加载，manifest 的 ID、版本、入口和 SDK 区间正确；
- 每个 Document/Tool 出现在预期菜单或 Dock 区域；
- 同一种 Document 打开两次时状态和 Scope 互不影响；
- Tool 隐藏后可恢复且 singleton 状态保留；
- 保存、恢复、关闭和生命周期行为符合插件声明；
- Host 没有报告共享程序集、私有依赖、入口类型或稳定 ID 错误；
- 替换为正式 ZIP 后完整重启 Host，并再次完成一次关键业务流程。

## Workbench Command G8 本地非发布验收

修改 13 个游戏的 Restart/Undo 工作台命令、SDK 3.3 引用或真实包边界时执行：

```powershell
pwsh -NoProfile -File .\scripts\Test-ClassicGameWorkbenchCommandG8.ps1 -Configuration Release
```

入口使用公开 NuGet、locked restore、全量测试/覆盖率、Standalone 构建和两轮确定性 ZIP，并把真实包输入交给
Host 的 `scripts/Test-WorkbenchCommandG8.ps1`。Release 仅表示本地编译配置；该入口不运行 Windows CI/Smoke、
Release Acceptance 或发布门禁，不上传、不签名、不打 tag。

## Workbench Command G10 本地封板

```powershell
pwsh -NoProfile -File .\scripts\Test-ClassicGameWorkbenchCommandG10.ps1 -Configuration Release
```

本入口让 G8 继续拥有十三游戏、覆盖率、确定性 ZIP 与 manifest 规则，只复核摘要和非发布边界。Host G10
会在两个独立工作树副本中调用它，并把本包与 WorkflowStudio 同时加载。它不调用 AIFLOW、Windows CI/Smoke、
Release Acceptance 或发布门禁，`publishable=false`。

## 常见注意事项

- Standalone 正常不代表 Host 一定能加载，优先检查正式 manifest、依赖边界和 SDK 区间。
- `plugin.manifest.json` 缺失或错误时重新执行 Build 目标，不要手工补写。
- 私有托管或原生依赖必须通过 Managed Plugin 构建协议声明，不能靠目录扫描碰运气加载。
- 当前只发布 `win-x64`，不要在同一个包里混入其他 RID 的原生资产。
- 调试目录可以改名，但稳定 Plugin ID 不能用目录名代替，也不能用复制副本的方式并行加载同一插件。
