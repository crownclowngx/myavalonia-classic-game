using MyAvaloniaManagement.PluginSdk;
using ClassicGamePlugin.Features.Xiangqi;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class XiangqiDocumentTests
{
    [Fact]
    public async Task 默认标题与Host标题遵循普通Document语义()
    {
        using var document = new XiangqiDocument();
        Assert.Equal("中国象棋", document.Presentation.Title);

        await document.InitializeAsync(new NewDocumentActivation("象棋练习"), CancellationToken.None);

        Assert.Equal("象棋练习", document.Presentation.Title);
    }

    [Fact]
    public void 多个Document拥有独立棋局()
    {
        using var first = new XiangqiDocument();
        using var second = new XiangqiDocument();

        first.ViewModel.PlayPosition(new(6, 0));
        first.ViewModel.PlayPosition(new(5, 0));

        Assert.Equal(1, first.ViewModel.MoveCount);
        Assert.Equal(0, second.ViewModel.MoveCount);
    }
}
