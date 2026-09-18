# 富翁小镇 G5：部署修复与 1.2.0 本地发布

日期：2026-09-18。分支：`codex/rich-town-stride`。前置开发提交：`1668ac9`。

用户在 G4/G5 开发后明确要求提交、部署到桌面 Controls，并进一步要求“解决这个部署问题，然后重新进行发布”。
因此本记录启用本地 Release 构建、测试、标准 ZIP 和指定目录部署；日常开发脚本仍默认 Debug。
没有使用 AIFLOW、Windows CI、统一 verify/seal、远端上传、签名或 tag。

## 1. 实际修复的三个问题

| 问题 | 原因 | 修复与责任边界 |
| --- | --- | --- |
| Build 拒绝 DependencyModel | 1.1.3 按 Microsoft.Extensions 家族禁带，而 Host 实际共享闭包不提供 DependencyModel | 主仓统一 RuntimeProfile 仅精确放行 `Microsoft.Extensions.DependencyModel.dll`，保留 DI/Configuration/SDK 等禁带；不改运行时 ALC |
| 资源重复目标路径 | 小镇部署声明在同一 ItemGroup 中使用未限定项名的 Filename/RecursiveDir，产生着色器与许可证交叉映射 | 小镇独立收集内容与许可证，再按各自项名映射；重复目标检查保持启用 |
| Host 中 SDL 初始化失败 | Silk 2.22 的默认原生查找使用入口进程依赖上下文，单文件 Host 的依赖表没有插件 SDL | 小镇渲染层在创建 SDL 窗口前配置自身私有 Silk 解析器，只映射 SDL2 的准确名称到本插件 `runtimes/win-x64/native/SDL2.dll` |

SDL 修复使用公开 `DefaultPathResolver.Resolvers` API，不修改进程 PATH、工作目录、Host 原生搜索路径或 NuGet 缓存。
配置由 Lazy 保证一次初始化；交付文件缺失时明确失败，不能借开发者缓存掩盖缺包。
规则、Document、存档与其他十四个游戏保持原有子领域边界，没有增加通用游戏加载框架。

上游行为依据：[Silk.NET 2.22 默认解析器源码](https://github.com/dotnet/Silk.NET/blob/v2.22.0/src/Core/Silk.NET.Core/Loader/DefaultPathResolver.cs)。
初次真实 Host 黑屏异常为 `Stride.Graphics.SDL.Window` 静态构造内 `Sdl.CreateDefaultContext` 找不到原生库。
失败产物和本机诊断保存在 `artifacts/rich-town-release/desktop-host-check/`，没有把失败轮次记为成功。

## 2. 版本与可复现输入

- 插件版本：`1.2.0`，SDK 区间 `[3.4.0, 4.0.0)`；Core/UI SDK 继续固定 `3.4.0`。
- Stride：`4.3.0.2507`；Avalonia：`12.1.0`；平台：win-x64。
- Build：`3.4.2-richtown.1`，固定在[仓库本地包源](../../../build/feed/README.md)，只用于构建。
- [nuget.config](../../../nuget.config) 对 Build 包 ID 做精确来源映射，其他包仍来自 NuGet.org；lock 文件同步。
- Build 源码修复位于主仓 `build/MyAvaloniaManagement.RuntimeProfile.props`，提交 `58d7bee`；候选工具包不是 NuGet.org 正式发布。
- 不升级桌面 Host 可执行文件；使用原有单文件 Host 3.0.0 验证新插件。

## 3. 本地发布复现

在插件根目录执行：

```powershell
dotnet restore ClassicGamePlugin.slnx --locked-mode -p:SkipPluginDeploy=true
dotnet build ClassicGamePlugin.slnx -c Release --no-restore -warnaserror -p:SkipPluginDeploy=true
```

完整单测沿用[开发检查](../../../scripts/Test-RichTownDevelopment.ps1)的三进程安排：
既有 555 项、魔方 Dispatcher 专项 1 项、小镇 225 项，合计 781 项。Release 测试将同一命令中的配置换为 Release，
保留覆盖率和千种子模拟；不能直接省略需要独立进程的魔方绑定用例。

```powershell
dotnet msbuild src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj `
  -t:BuildManagedPluginPackage -p:Configuration=Release -p:SkipPluginDeploy=true `
  -p:ManagedPluginPackageOutput=artifacts/rich-town-release/final-package

pwsh -NoProfile -File scripts/Test-RichTownPackage.ps1 `
  -Package artifacts/rich-town-release/final-package/ClassicGamePlugin.Plugin-1.2.0-win-x64.zip `
  -Manifest artifacts/rich-town-release/final-package/ClassicGamePlugin.Plugin-1.2.0-win-x64.manifest.json `
  -OutputDirectory artifacts/rich-town-release/final-package-check
```

`ManagedPluginPackageOutput` 建议传绝对路径，避免从其他工作目录调用时定位错误。每次包检查使用新的证据目录。
标准包构建入口负责 locked restore、严格清单、禁带/重复路径检查、确定性 ZIP、解压和 SHA-256 复核。
新增[实际包检查](../../../scripts/Test-RichTownPackage.ps1)再次核对 ZIP 路径集合、内外版本、逐文件摘要、
私有依赖闭包、400 个着色器与全部模型/许可证路径，防止预览目录通过但真正交付缺文件。

## 4. 验证记录

| 验证 | 结果 / 证据 |
| --- | --- |
| 主仓共享边界回归 | 30 项通过，包括新增 11 个准确例外/相似名拒绝/真实共享闭包用例；主仓 `artifacts/rich-town-deployment/policy-tests/policy.trx` |
| 首轮 Release 全量 | 原有 777 项通过、0 失败/跳过；`artifacts/rich-town-release/release-tests-20260918-174354/` |
| SDL 解析新增测试 | 4 项通过：中文空格目录、名称大小写、无关/越界名称不映射、缺文件/坏参数拒绝；`native-path-tests/native-paths.trx` |
| 最终 Debug 开发门禁 | 781 项通过，1,000 种子模拟、G2/G3/G4 覆盖率、资产转换、依赖审计和格式检查均通过；`artifacts/rich-town-tests/G5/development-20260918-180047-561/` |
| 最终 Release 回归 | 构建 0 警告/错误；781 项通过、0 失败/跳过，含 1,000 种子模拟；`final-release-tests/summary.json` |
| 发布包结构 | 462 文件、406 个内容/许可证文件（含 400 个着色器）；42 私有包与 7 原生文件均与锁定 NuGet 源摘要一致 |
| 原桌面 Host 的隔离副本 | 从功能中心打开小镇，棋盘就绪；实际点击掷骰得到 3 点、汽车到 03 休息区；结束回合后两名电脑继续 |
| 原生真实路径 | `desktop-host-sdl/loaded-modules.json`：SDL2、libstrideaudio、插件使用的 d3dcompiler 从本插件 RID 目录加载 |

完整本机证据根：`artifacts/rich-town-release/`。该目录不入 Git；复现命令和结论入 Git。
临时诊断工具与进程转储仅用于本机定位，不进入发布 ZIP。

## 5. 部署与回滚

目标固定为 `C:\Users\admin\Desktop\工作台\Controls\ClassicGamePlugin`。
安装时先确认目标 Host 已退出，核对最终绝对路径与清单，再将旧插件完整备份到 Controls 之外。
新目录整体替换，禁止把新旧文件合并，禁止清空 Controls 或留下两份相同插件 ID。
替换后逐文件核对发布清单，并核对其他插件与 Host 主程序摘要未变。
备份、替换前后摘要和实际版本由本次 `desktop-deployment/deployment-result.json` 记录。

回滚时先退出工作台，将当前 ClassicGamePlugin 移到 Controls 之外，再将报告指定的旧目录完整复制回
`Controls\ClassicGamePlugin`。旧版 1.1.1 不包含小镇，回滚后不要用旧版打开小镇存档。

## 6. 下一步路线与仍未签署的出口

本次解决的是合规打包、真实 Host 私有 SDL 加载和指定桌面部署；小镇仍保留“开发版”名称。
G5 不能因此整体签署，后续按顺序推进：

1. 原生 HWND 与 Avalonia 叠层合成：菜单、遮罩、浮动 Dock、多 DPI 与恢复矩阵。
2. Stride 只读安装目录初始化：消除 PlatformFolders 对程序目录 local/roaming/cache 的写入要求。
3. 20 次关闭循环的句柄增长：定位剩余原生引用，建立稳定平台后的资源曲线。
4. 真正净机与离线检查、VC++ `VCRUNTIME140.dll` 的交付/前提、许可证补充审核。
5. 真实 Host 文件选择、保存/恢复、第二实例拒绝和 Dock 生命周期的完整业务矩阵。
6. 上游 Build 正式发布包含本次规则后，移除本仓候选包源并升级锁文件。

本次没有宣称全平台、只读部署、完全自包含或完整 G5 通过。Windows、.NET、显卡驱动和 VC++ 前提仍见
[G5 历史记录](g5-local-integration.md)与[素材依赖清单](../../rich-town-assets-and-dependencies.md)。
