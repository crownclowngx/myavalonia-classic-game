using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClassicGamePlugin.Features.RichTown.Rendering;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

/// <summary>游戏子领域边界是结构约束：小镇不能引用其他游戏，其他游戏不能反向使用小镇实现。</summary>
public sealed class RichTownBoundaryTests
{
    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ClassicGamePlugin.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("找不到经典游戏仓库。");
    }

    [Fact]
    public void 小镇私有项目不反向引用插件且不包含其他游戏命名空间()
    {
        var root = RepositoryRoot();
        var renderer = Path.Combine(root, "src/ClassicGamePlugin.RichTown.Stride");
        var project = XDocument.Load(Path.Combine(renderer, "ClassicGamePlugin.RichTown.Stride.csproj"));
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.DoesNotContain(typeof(RichTownPrototypeViewport).Assembly.GetReferencedAssemblies(), a => a.Name == "ClassicGamePlugin.Plugin");
        foreach (var path in Sources(renderer).Concat(Sources(Path.Combine(root, "src/ClassicGamePlugin.Plugin/Features/RichTown"))))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotMatch(@"ClassicGamePlugin\.Features\.(?!RichTown\b)\w+", text);
            Assert.DoesNotContain("ClassicGamePlugin.Workbench", text);
        }
        foreach (var domain in Directory.GetDirectories(Path.Combine(root, "src/ClassicGamePlugin.Plugin/Features")))
            if (Path.GetFileName(domain) != "RichTown")
                foreach (var path in Sources(domain))
                    Assert.DoesNotMatch(@"ClassicGamePlugin\.Features\.RichTown\b|using\s+Stride\.", File.ReadAllText(path));
    }

    [Fact]
    public void 小镇规则和会话不依赖界面引擎文件时钟或其他子领域()
    {
        var feature = Path.Combine(RepositoryRoot(), "src/ClassicGamePlugin.Plugin/Features/RichTown");
        var sources = Sources(Path.Combine(feature, "Domain")).Concat(Sources(Path.Combine(feature, "Application"))).ToArray();
        Assert.NotEmpty(sources);
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotMatch(@"\b(Avalonia|Stride|MyAvaloniaManagement|System\.IO|System\.Threading)\.", text);
            Assert.DoesNotMatch(@"\b(File|Directory|DateTime|DateTimeOffset|Environment|Task|Thread)\.|Random\.Shared|new\s+Random\s*\(", text);
            Assert.DoesNotMatch(@"ClassicGamePlugin\.Features\.(?!RichTown\b)\w+", text);
        }
        Assert.Equal(File.ReadAllText(Path.Combine(feature, "Domain/Random-LICENSE.txt")),
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Licenses/RichTown/SplitMix64.txt")));
    }

    [Fact]
    public void 已下载素材及转换产物符合锁定摘要且可由运行时读取()
    {
        var root = Path.Combine(RepositoryRoot(), "src/ClassicGamePlugin.RichTown.Stride/Assets");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "asset-manifest.json")));
        foreach (var file in manifest.RootElement.GetProperty("files").EnumerateObject())
        {
            using var input = File.OpenRead(Path.Combine(root, file.Name));
            Assert.Equal(file.Value.GetString(), Convert.ToHexString(SHA256.HashData(input)));
        }
        using var mesh = File.OpenRead(Path.Combine(root, "Compiled/house.mesh"));
        Assert.Equal(7044, RichTownMeshData.Read(mesh).VertexCount);
        using var palette = File.OpenRead(Path.Combine(root, "Compiled/palette.rgba"));
        var pixels = RichTownMeshData.ReadPalette(palette);
        Assert.Equal(pixels.Width * pixels.Height * 4, pixels.Pixels.Length);
    }

    private static IEnumerable<string> Sources(string root) => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
        .Where(path => !Regex.IsMatch(path, @"[\\/](bin|obj)[\\/]"));
}
