using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Match3DocumentationTests
{
    [Fact]
    public void 专项文档记录固定规则SOLID特殊组合动画与非发布边界()
    {
        var root = TestRepository.Root;
        var document = File.ReadAllText(Path.Combine(root, "docs", "match3.md"));

        Assert.Contains("8×8", document, StringComparison.Ordinal);
        Assert.Contains("1500", document, StringComparison.Ordinal);
        Assert.Contains("SOLID", document, StringComparison.Ordinal);
        Assert.Contains("彩虹球 + 彩虹球", document, StringComparison.Ordinal);
        Assert.Contains("交换 120ms", document, StringComparison.Ordinal);
        Assert.Contains("不使用 AIFLOW", document, StringComparison.Ordinal);
        Assert.Contains("不运行 Windows CI/Smoke", document, StringComparison.Ordinal);
    }

    [Fact]
    public void 根说明与文档索引包含消消乐且全局数量为十三个()
    {
        var root = TestRepository.Root;
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var responsibilities = File.ReadAllText(
            Path.Combine(root, "docs", "project-and-window-responsibilities.md"));

        Assert.Contains("docs/match3.md", readme, StringComparison.Ordinal);
        Assert.Contains("十三个游戏", readme, StringComparison.Ordinal);
        Assert.Contains("(match3.md)", index, StringComparison.Ordinal);
        Assert.Contains("十三个标签页", responsibilities, StringComparison.Ordinal);
    }

}
