using ClassicGamePlugin.Features.ChineseCheckers;
using ClassicGamePlugin.Features.ChineseCheckers.ViewModels;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ChineseCheckersDocumentTests
{
    [Fact]
    public async Task 空白Host标题保留中国跳棋且非空标题只通知一次()
    {
        using var document = CreateDocument();
        var notifications = 0;
        document.PresentationChanged += (_, _) => notifications++;

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation("我的中国跳棋"), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation("我的中国跳棋"), CancellationToken.None);

        Assert.Equal("我的中国跳棋", document.Presentation.Title);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void 多Document棋局隔离且释放级联停止计时()
    {
        var first = CreateDocument();
        using var second = CreateDocument();
        var legal = ClassicGamePlugin.Features.ChineseCheckers.Domain.ChineseCheckersRules
            .GetLegalMoves(first.ViewModel.CurrentSnapshot)[0];
        first.ViewModel.SelectPosition(legal.From);
        first.ViewModel.SelectPosition(legal.To);

        Assert.Equal(1, first.ViewModel.MoveCount);
        Assert.Equal(0, second.ViewModel.MoveCount);
        first.Dispose();
        Assert.False(first.ViewModel.IsTimerRunning);
        Assert.IsNotAssignableFrom<IPluginDocument>(first.ViewModel);
    }

    private static ChineseCheckersDocument CreateDocument() =>
        new(new ManualTimeProvider(), false, ChineseCheckersTestData.FirstLegalStrategies());
}
