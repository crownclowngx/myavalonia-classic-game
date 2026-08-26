using ClassicGamePlugin.Features.Reversi;
using ClassicGamePlugin.Features.Reversi.ViewModels;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ReversiDocumentTests
{
    [Fact]
    public async Task 空白Host标题保留黑白棋默认标题()
    {
        using var document = CreateDocument();

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);

        Assert.Equal("黑白棋", document.Presentation.Title);
    }

    [Fact]
    public async Task 初始化采用Host标题并通知展示变化()
    {
        using var document = CreateDocument();
        var notificationCount = 0;
        document.PresentationChanged += (_, _) => notificationCount++;

        await document.InitializeAsync(new NewDocumentActivation("我的黑白棋"), CancellationToken.None);

        Assert.Equal("我的黑白棋", document.Presentation.Title);
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public void Document只拥有独立ViewModel且释放会停止计时()
    {
        var document = CreateDocument();
        document.ViewModel.PlayCell(FindCell(document.ViewModel, 2, 3));
        Assert.True(document.ViewModel.IsTimerRunning);

        document.Dispose();

        Assert.False(document.ViewModel.IsTimerRunning);
        Assert.IsNotAssignableFrom<IPluginDocument>(document.ViewModel);
        Assert.Null(typeof(ReversiDocument).GetProperty("BoardCells"));
        Assert.Null(typeof(ReversiDocument).GetProperty("HintCommand"));
    }

    private static ReversiDocument CreateDocument() =>
        new(new ManualTimeProvider(), false, ReversiTestStrategies.CreateFirstLegal());

    private static ReversiCellViewModel FindCell(
        ReversiViewModel viewModel,
        int row,
        int column) =>
        Assert.Single(viewModel.BoardCells, cell => cell.Row == row && cell.Column == column);
}
