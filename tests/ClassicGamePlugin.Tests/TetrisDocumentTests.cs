using ClassicGamePlugin.Features.Tetris;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class TetrisDocumentTests
{
    [Fact]
    public async Task 默认标题可由Host激活标题替换且空白标题保持不变()
    {
        var document = new TetrisDocument();
        var changes = 0;
        document.PresentationChanged += (_, _) => changes++;

        await document.InitializeAsync(new NewDocumentActivation("我的俄罗斯方块"), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation(" "), CancellationToken.None);

        Assert.Equal("我的俄罗斯方块", document.Presentation.Title);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void 每个Document创建独立对局且只释放工作台订阅()
    {
        var first = new TetrisDocument();
        var second = new TetrisDocument();
        first.ViewModel.AnimationsEnabled = false;
        first.ViewModel.HardDropCommand.Execute(null);

        Assert.NotSame(first.ViewModel, second.ViewModel);
        Assert.True(first.ViewModel.Score > second.ViewModel.Score);
        Assert.IsAssignableFrom<IDisposable>(first);
        first.Dispose();
        second.Dispose();
    }
}
