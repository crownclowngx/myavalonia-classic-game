# 小镇部署修复构建包

`MyAvaloniaManagement.Plugin.Build.3.4.2-richtown.1.nupkg` 是本仓固定使用的本地构建工具候选包，
不上传 NuGet.org，不进入插件运行时目录。`nuget.config` 将这个准确包 ID 映射到本目录，
因此其他机器检出仓库后也能 locked restore，无须修改全局 NuGet 缓存。

- SHA-256：`F988301C403D039004F7188783BD1CEC62EFFCE7E62BB31C4B5233E1D8150E2F`。
- 来源：主仓 `avalonia_dock_simple_test`，基线 `554e3750de10927d0ac964e212c70528c65f6f79`；本次策略修复提交 `58d7bee`。
- 构建项目：`Packaging/MyAvaloniaManagement.Plugin.Build/MyAvaloniaManagement.Plugin.Build.csproj`。
- 修改：统一 RuntimeProfile 精确允许 `Microsoft.Extensions.DependencyModel.dll` 私有交付；
  仍拒绝 DI、Configuration、SDK、Avalonia 以及 `DependencyModel.Extra.dll` 等相似名称。
- 候选包同时包含主仓现有构建协议（含 `plugin.build.json`），并未修改 SDK 引用或插件程序集加载器。
- 根因、测试、实际交付与已知限制见[部署修复记录](../../docs/plan-history/rich-town/g5-deployment-repair.md)。

在含此修复的主仓重新构建：

```powershell
dotnet pack Packaging/MyAvaloniaManagement.Plugin.Build/MyAvaloniaManagement.Plugin.Build.csproj `
  -c Release -p:MyAvaloniaPluginBuildVersion=3.4.2-richtown.1 `
  -p:SkipPluginDeploy=true -o artifacts/rich-town-build-feed
```

包内 `tools/MyAvaloniaManagement.RuntimeProfile.props` 为实际生效规则；包摘要及 lock 文件共同固定输入。
重新生成的包如字节变化，必须使用新的候选版本、同步摘要和 lock，不能覆盖已经固定的同版本包。
待正式 Build 包包含同一修复后，升级到正式版本并移除此本地源。
