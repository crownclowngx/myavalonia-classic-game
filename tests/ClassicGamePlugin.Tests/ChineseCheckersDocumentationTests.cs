using System.Runtime.CompilerServices;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ChineseCheckersDocumentationTests
{
    [Fact]
    public void 专项文档固定规则SOLID设计思路动画与非发布边界()
    {
        var root = FindRepositoryRoot();
        var document = File.ReadAllText(Path.Combine(root, "docs", "chinese-checkers.md"));

        Assert.Contains("121 孔", document, StringComparison.Ordinal);
        Assert.Contains("强制撤营", document, StringComparison.Ordinal);
        Assert.Contains("SOLID", document, StringComparison.Ordinal);
        Assert.Contains("设计思路", document, StringComparison.Ordinal);
        Assert.Contains("120ms", document, StringComparison.Ordinal);
        Assert.Contains("160ms", document, StringComparison.Ordinal);
        Assert.Contains("不使用 AIFLOW", document, StringComparison.Ordinal);
        Assert.Contains("不增加或运行 Windows CI", document, StringComparison.Ordinal);
        Assert.Contains("不执行 Release", document, StringComparison.Ordinal);
    }

    [Fact]
    public void 根说明索引与窗口职责包含中国跳棋且总数为十三个()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var responsibilities = File.ReadAllText(
            Path.Combine(root, "docs", "project-and-window-responsibilities.md"));

        Assert.Contains("docs/chinese-checkers.md", readme, StringComparison.Ordinal);
        Assert.Contains("十三个游戏", readme, StringComparison.Ordinal);
        Assert.Contains("(chinese-checkers.md)", index, StringComparison.Ordinal);
        Assert.Contains("十三个标签页", responsibilities, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        if (File.Exists(Path.Combine(Environment.CurrentDirectory, "ClassicGamePlugin.slnx")))
        {
            return Environment.CurrentDirectory;
        }

        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFilePath) ?? AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ClassicGamePlugin.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("未找到 ClassicGamePlugin 仓库根目录。");
    }
}
