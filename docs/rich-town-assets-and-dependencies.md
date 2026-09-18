# 富翁小镇 G1：素材与依赖清单

日期：2026-09-18。当前为开发原型，**部署阻塞，未证明自包含**。
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
```

Stride MIT 原文来自锁定源码修订 [e023d874… 的 LICENSE.md](https://github.com/stride3d/stride/blob/e023d874ba2985dbb132e9608f00d28d021b6f78/LICENSE.md)，
随运行内容保留。其他传递包和原生组件的许可尚需逐项完成最终分发审查；上面的两份许可证不能覆盖所有第三方组件。

第一次运行曾因缺少 `LightDirectionalGroup.sdsl` 失败，已修正为显式携带 400 个同版着色器源。
程序为当前表面创建 `%TEMP%/ClassicGamePlugin/RichTown/<随机目录>` 的独立 ObjectDatabase，
将 shader 映射到 `shaders/文件名`；关闭时释放文件提供器并只清理本实例创建的目录。
尚未执行安装目录只读、非 ASCII 路径或无开发环境的完整检查。

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
操作系统 Direct3D、显卡驱动、.NET 运行时属于平台前提；VC++ 运行库和原生导入表的二级依赖仍待验证。
不得将当前开发机上的运行成功表述为“无需任何外部依赖”。

## 已证实的部署冲突

依赖链之一为 `Stride.Engine → Stride.VirtualReality → Silk.NET.OpenXR → Silk.NET.Core → Microsoft.Extensions.DependencyModel`。
当前 Build 1.1.3 拒绝任何 `Microsoft.Extensions.*` DLL 进入插件目录，目标 Host 的 Debug 输出中没有 DependencyModel。
保持全部声明后执行隔离 Debug 部署，明确失败于 `Microsoft.Extensions.DependencyModel.dll`，尚未产生有效暂存目录。
本轮没有删掉 DLL、修改 Build 的禁带规则、修改 Host 共享闭包或复制整个 bin 来绕过协议。

下一次 G1 应先确定公开受支持的依赖交付方案，随后重新核对托管闭包、原生导入表、实际加载路径、离线和只读路径。
同进程的原生库可能驻留到进程退出；目前没有可卸载或多版本原生隔离的保证。
