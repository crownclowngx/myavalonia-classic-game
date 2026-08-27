using ClassicGamePlugin.Features.Sokoban;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SokobanDocumentTests
{
    [Fact]
    public async Task 默认标题可由Host激活标题替换且空白标题保持不变()
    {
        var document = new SokobanDocument();
        var changes = 0;
        document.PresentationChanged += (_, _) => changes++;
        Assert.Equal("推箱子", document.Presentation.Title);

        await document.InitializeAsync(new NewDocumentActivation("我的推箱子"), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation(" "), CancellationToken.None);

        Assert.Equal("我的推箱子", document.Presentation.Title);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void 每个Document创建独立ViewModel()
    {
        var first = new SokobanDocument();
        var second = new SokobanDocument();

        Assert.NotSame(first.ViewModel, second.ViewModel);
        first.ViewModel.AnimationsEnabled = false;
        first.ViewModel.MoveDownCommand.Execute(null);
        Assert.Equal(1, first.ViewModel.MoveCount);
        Assert.Equal(0, second.ViewModel.MoveCount);
    }
}
