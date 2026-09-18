using ClassicGamePlugin.Features.RichTown.Rendering;
using Silk.NET.Core.Loader;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

/// <summary>只验证路径解析，不加载测试用空 DLL；实际原生加载由正式 ZIP 的真实 Host 检查负责。</summary>
public sealed class RichTownNativeLibraryPathsTests
{
    [Theory]
    [InlineData("SDL2")]
    [InlineData("SDL2.dll")]
    [InlineData("sdl2.DLL")]
    public void 中文空格目录优先解析本插件原生文件(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "小镇 原生测试", Guid.NewGuid().ToString("N"));
        var native = Path.Combine(root, "runtimes", "win-x64", "native");
        Directory.CreateDirectory(native);
        var file = Path.Combine(native, "SDL2.dll");
        File.WriteAllBytes(file, []);
        try
        {
            var resolver = new DefaultPathResolver();
            var originalCount = resolver.Resolvers.Count;
            RichTownNativeLibraryPaths.Configure(resolver, root);
            Assert.Equal(originalCount + 1, resolver.Resolvers.Count);
            Assert.Equal(file, resolver.EnumeratePossibleLibraryLoadTargets(name).First());
            Assert.Empty(resolver.Resolvers[0]("other.dll"));
            Assert.Empty(resolver.Resolvers[0]("../SDL2.dll"));
            Assert.Empty(resolver.Resolvers[0]("SDL2-extra.dll"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void 缺少交付文件直接失败并保留原解析器()
    {
        var resolver = new DefaultPathResolver();
        var original = resolver.Resolvers.ToArray();
        Assert.Throws<FileNotFoundException>(() => RichTownNativeLibraryPaths.Configure(resolver, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        Assert.Equal(original, resolver.Resolvers);
        Assert.Throws<ArgumentNullException>(() => RichTownNativeLibraryPaths.Configure(null!, "."));
        Assert.Throws<ArgumentException>(() => RichTownNativeLibraryPaths.Configure(resolver, " "));
    }
}
