using Avalonia;
using ClassicGamePlugin.Features.Match3;
using ClassicGamePlugin.Features.Match3.Domain;
using ClassicGamePlugin.Features.Match3.ViewModels;
using ClassicGamePlugin.Features.Match3.Views;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Match3DocumentTests
{
    [Fact]
    public async Task 空白标题保留默认值且Host标题只在变化时通知()
    {
        var document = CreateDocument();
        var count = 0;
        document.PresentationChanged += (_, _) => count++;

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation("我的消消乐"), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation("我的消消乐"), CancellationToken.None);

        Assert.Equal("我的消消乐", document.Presentation.Title);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Document只拥有独立ViewModel且释放职责限于工作台订阅()
    {
        var first = CreateDocument();
        var second = CreateDocument();

        Assert.IsType<Match3ViewModel>(first.ViewModel);
        Assert.NotSame(first.ViewModel, second.ViewModel);
        Assert.IsNotAssignableFrom<IPersistablePluginDocument>(first);
        Assert.IsAssignableFrom<IDisposable>(first);
        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public void 棋盘命中测试覆盖边界并拒绝棋盘外位置()
    {
        Assert.True(Match3BoardControl.TryHitTest(new Size(500, 500), new Point(250, 250), out var center));
        Assert.True(Match3Rules.IsInside(center));
        Assert.False(Match3BoardControl.TryHitTest(new Size(500, 500), new Point(2, 2), out _));
        Assert.False(Match3BoardControl.TryHitTest(new Size(500, 500), new Point(510, 250), out _));
    }

    private static Match3Document CreateDocument() =>
        new(new Match3ViewModel(new Match3Game(new CyclingMatch3Random(), Match3Boards.Stable())));
}
