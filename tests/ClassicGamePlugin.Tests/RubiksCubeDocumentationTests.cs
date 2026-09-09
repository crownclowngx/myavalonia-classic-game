using System.Text.RegularExpressions;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class RubiksCubeDocumentationTests
{
    [Fact]
    public void 专用文档覆盖规则设计思路操作与开发边界()
    {
        var document = File.ReadAllText(Path.Combine(TestRepository.Root, "docs", "rubiks-cube.md"));
        foreach (var required in new[] { "SOLID", "设计思路", "ICubeTeachingSolver", "300ms", "1,000", "退回", "逐组",
                     "不使用 AIFLOW", "不新增或运行 Windows CI", "--locked-mode", "SkipPluginDeploy=true", "本次验证记录" })
            Assert.Contains(required, document, StringComparison.Ordinal);
        Assert.Contains("docs/rubiks-cube.md", File.ReadAllText(Path.Combine(TestRepository.Root, "README.md")), StringComparison.Ordinal);
        Assert.Contains("(rubiks-cube.md)", File.ReadAllText(Path.Combine(TestRepository.Root, "docs", "README.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void 修改的文档中所有本地链接均指向存在的文件()
    {
        foreach (var relative in new[] { "README.md", "docs/README.md", "docs/rubiks-cube.md", "docs/project-and-window-responsibilities.md", "docs/workbench-commands.md" })
        {
            var path = Path.Combine(TestRepository.Root, relative);
            foreach (Match match in Regex.Matches(File.ReadAllText(path), @"\[[^\]]*\]\(([^)]+)\)"))
            {
                var link = match.Groups[1].Value;
                if (link.Contains("://", StringComparison.Ordinal) || link.StartsWith('#')) continue;
                link = Uri.UnescapeDataString(link.Split('#')[0]);
                Assert.True(File.Exists(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, link))), $"失效链接：{relative} → {link}");
            }
        }
    }
}
