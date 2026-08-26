using ClassicGamePlugin.Features.Gomoku;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GomokuDocumentTests
{
    [Fact]
    public async Task 空白Host标题保留默认标题且非空标题触发一次通知()
    {
        using var document = CreateDocument();
        var notifications = 0;
        document.PresentationChanged += (_, _) => notifications++;

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);
        Assert.Equal("五子棋", document.Presentation.Title);

        await document.InitializeAsync(new NewDocumentActivation("我的五子棋"), CancellationToken.None);
        Assert.Equal("我的五子棋", document.Presentation.Title);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void Document只拥有独立ViewModel且释放会停止计时()
    {
        var document = CreateDocument();
        document.ViewModel.PlayPosition(new(0, 0));
        Assert.True(document.ViewModel.IsTimerRunning);

        document.Dispose();

        Assert.False(document.ViewModel.IsTimerRunning);
        Assert.IsNotAssignableFrom<IPluginDocument>(document.ViewModel);
        Assert.Null(typeof(GomokuDocument).GetProperty("UndoCommand"));
    }

    private static GomokuDocument CreateDocument() =>
        new(new ManualTimeProvider(), false, GomokuTestStrategies.CreateFirstLegal());
}
