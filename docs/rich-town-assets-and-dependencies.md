# 富翁小镇：素材与依赖清单

日期：2026-09-18。当前 G4 已实现、G5 已开展审计；**部署和 Host SDL 加载已修复，只读与完整自包含验收仍未通过**。
1.2.0 发布包及真实 Host 棋盘/回合证据见[部署修复记录](plan-history/rich-town/g5-deployment-repair.md)。
关联：[设计方案](rich-town.md)、[G1 集成记录](plan-history/rich-town/g1-stride-integration.md)。

## 已纳入的一栋房屋

| 项目 | 实际值 |
| --- | --- |
| 作者、资源包 | Kenney，City Kit (Suburban) 2.0 |
| 官方页面 | [City Kit (Suburban)](https://kenney.nl/assets/city-kit-suburban) |
| 下载及记录日期 | 2026-09-18 |
| 许可 | 原包 License.txt 明确为 CC0；保留原文，不将免费价格等同于许可 |
| 使用文件 | building-type-a.obj、building-type-a.mtl、colormap.png、License.txt |
| 游戏显示内容 | 单栋房屋的网格和调色板；未使用其他模型、车、音频或商标 |
| 运行产物 | house.mesh：7,044 个展开顶点；palette.rgba：小端头和原始 RGBA 像素 |

原文件位于 [Assets/Source](../src/ClassicGamePlugin.RichTown.Stride/Assets/Source)，
来源 URL、压缩包 SHA-256、六个源码/转换文件的实际 SHA-256 位于
[asset-manifest.json](../src/ClassicGamePlugin.RichTown.Stride/Assets/asset-manifest.json)。
没有把整份压缩包或未经使用的模型放进运行目录。

[转换脚本](../scripts/Convert-RichTownPrototypeAsset.ps1)仅在开发期运行，不是通用 OBJ 导入器。
它按此模型的凸面做固定扇形三角化，保留位置/法线/UV，翻转 V，并从 PNG 提取 RGBA；忽略 OBJ 附加顶点 RGB，采用作者的调色板纹理。
不写时间戳，专用开发检查会在临时证据目录再次转换并比对摘要。播放器无需 OBJ/PNG 导入库或 Game Studio。

## 实际资源路径与许可

运行时从 `ClassicGamePlugin.RichTown.Stride.dll` 所在目录定位 `Assets/RichTown`，不使用 Host 的 BaseDirectory 代替插件目录：

```text
Assets/RichTown/house.mesh
Assets/RichTown/palette.rgba
Assets/RichTown/Shaders/*.sdsl      # 400 个，来自同版 Graphics 和 Rendering 包
Licenses/RichTown/Kenney.txt
Licenses/RichTown/Stride.txt
Licenses/RichTown/asset-manifest.json
Licenses/RichTown/SplitMix64.txt   # G2 纯托管随机算法来源许可
```

Stride MIT 原文来自锁定源码修订 [e023d874… 的 LICENSE.md](https://github.com/stride3d/stride/blob/e023d874ba2985dbb132e9608f00d28d021b6f78/LICENSE.md)，
随运行内容保留。G2 的 SplitMix64 改编自 [Sebastiano Vigna 的公开参考实现](https://prng.di.unimi.it/splitmix64.c)，
保留[原始许可声明与改编说明](../src/ClassicGamePlugin.Plugin/Features/RichTown/Domain/Random-LICENSE.txt)，通过现有小镇部署声明复制到许可证目录。
G2 未新增 NuGet、原生库或模型，G1 机器依赖清单保持历史内容。其他传递包和原生组件的许可仍需逐项完成最终分发审查。

G3 未新增下载或依赖，继续复用同一房屋与调色板：一级一栋、二级两栋。棋盘、七段编号、骰子/骰点、汽车/车号和树木由
[小镇场景代码](../src/ClassicGamePlugin.RichTown.Stride/RichTownBoardScene.cs)生成，没有引入 Car Kit/Roads 或新的外部许可。
场景共用基础立方体缓冲和按颜色缓存的模型，产权模型组合只随规则修订更新；资源仍由当前表面的 Stride Game 统一释放。
Standalone 的 `app.manifest` 仅补充原生子窗口所需的 Windows 兼容性声明，不增加管理员权限，不改变插件资产路径或 Host 策略。
真实窗口结果见[G3 专项记录](plan-history/rich-town/g3-playable-scene.md)；后续部署修复不代替离线与只读验证。

第一次运行曾因缺少 `LightDirectionalGroup.sdsl` 失败，已修正为显式携带 400 个同版着色器源。
程序为当前表面创建 `%TEMP%/ClassicGamePlugin/RichTown/<随机目录>` 的独立 ObjectDatabase，
将 shader 映射到 `shaders/文件名`；关闭时释放文件提供器并只清理本实例创建的目录。
G5 中文/空格可写目录 20 次窗口循环通过；只读程序目录因 Stride PlatformFolders 静态初始化创建 local/roaming/cache 失败。
这与上述独立 ObjectDatabase 缓存不是同一职责；不能据 TEMP 缓存宣称安装目录无需写入。无开发环境的净机检查仍未通过。

## NuGet 与原生文件盘点

版本事实由 `Directory.Packages.props` 和锁文件维护：Stride 引擎系列 4.3.0.2507，独立 SharpFont 包 1.0.1。
小镇私有 [部署声明](../src/ClassicGamePlugin.Plugin/Features/RichTown/RichTown.Deployment.targets)列出 42 个运行依赖包，
没有假定顶层 Engine 会自动为 Build 协议包含传递依赖。源于 `project.assets.json` 和已还原包元数据的
[G1 机器可读盘点](plan-history/rich-town/g1-dependency-inventory.json)记录包版本、NuGet 内容哈希、许可声明及运行时文件。
它是待验证清单，不是通过验收的包内容证明；许可字段为空时必须继续核对 URL/原包。

| 包 | win-x64 原生文件 |
| --- | --- |
| Ultz.Native.SDL 2.30.8 | SDL2.dll |
| Stride.Graphics 4.3.0.2507 | freetype.dll |
| Stride.Shaders.Compiler 4.3.0.2507 | d3dcompiler_47.dll |
| Stride.Audio 4.3.0.2507 | libstrideaudio.dll |
| Stride.VirtualReality 4.3.0.2507 | libstridevr.dll、openvr_api.dll、openxr_loader.dll |

这些文件在 NuGet 图中位于 `runtimes/win-x64/native`。未主动使用 VR/音频不等于可以删除它们；G1 保留完整声明。
操作系统 Direct3D、显卡驱动、.NET 运行时属于平台前提。G5 已解析 7 个原生文件的普通/延迟导入，
libstrideaudio、freetype、libstridevr 依赖 VCRUNTIME140.dll；本机实际从 System32 加载，尚未携带合法可再分发副本。
新 [审计脚本](../scripts/Test-RichTownDependencies.ps1)从实际锁定图生成版本/摘要/导入与许可元数据，结果和边界见 [G5 记录](plan-history/rich-town/g5-local-integration.md)。
不得将当前开发机上的运行成功表述为“无需任何外部依赖”。

## 历史部署冲突与本次修复

依赖链之一为 `Stride.Engine → Stride.VirtualReality → Silk.NET.OpenXR → Silk.NET.Core → Microsoft.Extensions.DependencyModel`。
此前 Build 1.1.3 拒绝任何 `Microsoft.Extensions.*` DLL 进入插件目录，目标 Host 共享闭包中没有 DependencyModel，导致隔离部署失败。
本次通过主仓 RuntimeProfile 精确放行该私有程序集，并固定使用 `3.4.2-richtown.1` 构建候选包；没有扩大整个 Extensions 家族的许可或修改 Host 加载器。
同时修正内容映射与 Silk SDL 路径解析，标准 ZIP 含 462 个文件；实际 Host 已从插件 RID 目录加载 SDL2、音频库和着色器编译库并推进人机回合。

下一步继续完成原生二级依赖的净机/离线检查、只读路径与许可分发审查；正式 Build 包包含该修复后替换本地候选包。
同进程的原生库可能驻留到进程退出；目前没有可卸载或多版本原生隔离的保证。
