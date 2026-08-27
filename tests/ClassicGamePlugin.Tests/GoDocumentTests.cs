using ClassicGamePlugin.Features.Go;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GoDocumentTests
{
    [Fact]
    public async Task 空白Host标题保留围棋且非空标题只触发一次通知()
    {
        using var document = new GoDocument(new ManualTimeProvider(), false);
        var notifications = 0;
        document.PresentationChanged += (_, _) => notifications++;

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);
        Assert.Equal("围棋", document.Presentation.Title);

        await document.InitializeAsync(new NewDocumentActivation("我的围棋"), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation("我的围棋"), CancellationToken.None);
        Assert.Equal("我的围棋", document.Presentation.Title);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void 每个Document拥有独立ViewModel且释放级联停止计时()
    {
        var first = new GoDocument(new ManualTimeProvider(), false);
        using var second = new GoDocument(new ManualTimeProvider(), false);
        first.ViewModel.PlayPosition(3, 3);

        Assert.Equal(1, first.ViewModel.MoveCount);
        Assert.Equal(0, second.ViewModel.MoveCount);
        Assert.True(first.ViewModel.IsTimerRunning);

        first.Dispose();

        Assert.False(first.ViewModel.IsTimerRunning);
        Assert.IsNotAssignableFrom<IPluginDocument>(first.ViewModel);
        Assert.Null(typeof(GoDocument).GetProperty("UndoCommand"));
    }
}
