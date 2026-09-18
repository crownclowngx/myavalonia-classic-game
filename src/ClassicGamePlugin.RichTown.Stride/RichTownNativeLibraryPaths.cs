using Silk.NET.Core.Loader;

namespace ClassicGamePlugin.Features.RichTown.Rendering;

/// <summary>
/// 为小镇私有的 Silk SDL 动态加载补充插件目录。Silk 2.22 默认查看入口进程的 deps，
/// 在单文件 Host 中看不到插件的 SDL；此处只映射 SDL2 的准确名称，不修改 PATH、当前目录或 Host 加载策略。
/// Silk.NET.Core 本身属于本插件 ALC，因此解析器配置也只影响本插件，其他游戏不依赖此类型。
/// </summary>
internal static class RichTownNativeLibraryPaths
{
    private static readonly Lazy<bool> Configured = new(() =>
    {
        Configure((DefaultPathResolver)PathResolver.Default,
            Path.GetDirectoryName(typeof(RichTownNativeLibraryPaths).Assembly.Location)!);
        return true;
    });

    /// <summary>必须早于 Stride SDL.Window 的静态构造；成功后只安装一次，不在每帧或每次开窗重复注册。</summary>
    internal static void EnsureConfigured() => _ = Configured.Value;

    internal static void Configure(DefaultPathResolver resolver, string pluginDirectory)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        var library = Path.GetFullPath(Path.Combine(pluginDirectory, "runtimes", "win-x64", "native", "SDL2.dll"));
        // 缺少交付文件直接失败，不能碰巧从开发者的 NuGet 缓存或另一个插件取得 SDL 而误判自包含。
        if (!File.Exists(library)) throw new FileNotFoundException("小镇交付目录缺少 win-x64 SDL2.dll。", library);
        resolver.Resolvers.Insert(0, name =>
            string.Equals(name, "SDL2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "SDL2.dll", StringComparison.OrdinalIgnoreCase)
                ? [library] : []);
    }
}
