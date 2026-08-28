using System.Runtime.CompilerServices;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GoDocumentationTests
{
    [Fact]
    public void 专项文档记录规则SOLID动画测试与明确非发布边界()
    {
        var root = FindRepositoryRoot();
        var document = File.ReadAllText(Path.Combine(root, "docs", "go.md"));

        Assert.Contains("SOLID 职责与朴素设计", document, StringComparison.Ordinal);
        Assert.Contains("位置全局同形禁着", document, StringComparison.Ordinal);
        Assert.Contains("中国数子法", document, StringComparison.Ordinal);
        Assert.Contains("7.5", document, StringComparison.Ordinal);
        Assert.Contains("GoAnimationPlan", document, StringComparison.Ordinal);
        Assert.Contains("自动化测试与开发门禁", document, StringComparison.Ordinal);
        Assert.Contains("Windows CI", document, StringComparison.Ordinal);
        Assert.Contains("不使用", document, StringComparison.Ordinal);
        Assert.Contains("AIFLOW", document, StringComparison.Ordinal);
    }

    [Fact]
    public void 根说明文档索引与窗口职责同步为十三个游戏并链接围棋文档()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var responsibilities = File.ReadAllText(Path.Combine(root, "docs", "project-and-window-responsibilities.md"));

        Assert.Contains("docs/go.md", readme, StringComparison.Ordinal);
        Assert.Contains("十三个游戏", readme, StringComparison.Ordinal);
        Assert.Contains("(go.md)", index, StringComparison.Ordinal);
        Assert.Contains("十三个标签页", responsibilities, StringComparison.Ordinal);
    }

    [Fact]
    public void Standalone显式创建并释放围棋Document()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "ClassicGamePlugin.Standalone", "MainWindow.axaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "src", "ClassicGamePlugin.Standalone", "MainWindow.axaml.cs"));

        Assert.Contains("Header=\"围棋\"", view, StringComparison.Ordinal);
        Assert.Contains("GoDocumentView", view, StringComparison.Ordinal);
        Assert.Contains("new GoDocument()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_goDocument.Dispose()", codeBehind, StringComparison.Ordinal);
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

        return directory?.FullName ?? throw new DirectoryNotFoundException("未找到 ClassicGamePlugin 解决方案根目录。");
    }
}
