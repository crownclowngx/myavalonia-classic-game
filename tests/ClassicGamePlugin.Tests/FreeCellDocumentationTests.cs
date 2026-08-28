using System.Runtime.CompilerServices;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class FreeCellDocumentationTests
{
    [Fact]
    public void 专项文档记录规则设计求解动画测试与非发布边界()
    {
        var root = FindRepositoryRoot();
        var document = File.ReadAllText(Path.Combine(root, "docs", "freecell.md"));

        Assert.Contains("SOLID 职责与朴素设计", document, StringComparison.Ordinal);
        Assert.Contains("(空闲单元数 + 1) × 2^可用空列数", document, StringComparison.Ordinal);
        Assert.Contains("300,000", document, StringComparison.Ordinal);
        Assert.Contains("FreeCellCardControl", document, StringComparison.Ordinal);
        Assert.Contains("自动化测试与开发门禁", document, StringComparison.Ordinal);
        Assert.Contains("不配置或运行 Windows CI", document, StringComparison.Ordinal);
        Assert.Contains("不使用 AIFLOW", document, StringComparison.Ordinal);
    }

    [Fact]
    public void 根说明与文档索引链接空当接龙专项文档并更新为十三个游戏()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var responsibilities = File.ReadAllText(Path.Combine(root, "docs", "project-and-window-responsibilities.md"));

        Assert.Contains("docs/freecell.md", readme, StringComparison.Ordinal);
        Assert.Contains("十三个游戏", readme, StringComparison.Ordinal);
        Assert.Contains("(freecell.md)", index, StringComparison.Ordinal);
        Assert.Contains("十三个标签页", responsibilities, StringComparison.Ordinal);
    }

    [Fact]
    public void Standalone在窗口打开后异步初始化空当接龙以免阻塞UI线程()
    {
        var root = FindRepositoryRoot();
        var codeBehind = File.ReadAllText(
            Path.Combine(root, "src", "ClassicGamePlugin.Standalone", "MainWindow.axaml.cs"));

        Assert.Contains("Opened += OnOpened;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private async void OnOpened", codeBehind, StringComparison.Ordinal);
        Assert.Contains("await _freeCellDocument.InitializeAsync(", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new NewDocumentActivation(\"空当接龙（Standalone）\"),\n            CancellationToken.None).GetAwaiter().GetResult();",
            codeBehind,
            StringComparison.Ordinal);
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
