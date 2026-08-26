using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.Minesweeper.Domain;
using ClassicGamePlugin.Features.Minesweeper.ViewModels;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class MinesweeperDocumentTests
{
    [Fact]
    public async Task 空白Host标题保留扫雷默认标题()
    {
        using var document = CreateDocument();

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);

        Assert.Equal("扫雷", document.Presentation.Title);
    }

    [Fact]
    public async Task 初始化时采用Host提供的标题并通知展示变化()
    {
        using var document = CreateDocument();
        var notificationCount = 0;
        document.PresentationChanged += (_, _) => notificationCount++;

        await document.InitializeAsync(new NewDocumentActivation("我的扫雷"), CancellationToken.None);

        Assert.Equal("我的扫雷", document.Presentation.Title);
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public void Document只暴露独立ViewModel而不承载展示属性()
    {
        using var document = CreateDocument();

        Assert.NotNull(document.ViewModel);
        Assert.IsNotAssignableFrom<IPluginDocument>(document.ViewModel);
        Assert.Null(typeof(MinesweeperDocument).GetProperty("BoardCells"));
        Assert.Null(typeof(MinesweeperDocument).GetProperty("RestartCommand"));
    }

    [Fact]
    public void 释放Document会级联停止ViewModel计时器()
    {
        var document = CreateDocument();
        document.ViewModel.RevealCell(FindCell(document.ViewModel, 8, 8));
        Assert.True(document.ViewModel.IsTimerRunning);

        document.Dispose();

        Assert.False(document.ViewModel.IsTimerRunning);
    }

    private static MinesweeperDocument CreateDocument() =>
        new(
            CreateBarrierMinePlacementStrategy(),
            new ManualTimeProvider(),
            false);

    internal static FixedMinePlacementStrategy CreateBarrierMinePlacementStrategy() =>
        new(
            new CellCoordinate(0, 0),
            new CellCoordinate(1, 0),
            new CellCoordinate(1, 1),
            new CellCoordinate(1, 2),
            new CellCoordinate(1, 3),
            new CellCoordinate(1, 4),
            new CellCoordinate(1, 5),
            new CellCoordinate(1, 6),
            new CellCoordinate(1, 7),
            new CellCoordinate(1, 8));

    private static MinesweeperCellViewModel FindCell(
        MinesweeperViewModel viewModel,
        int row,
        int column) =>
        Assert.Single(viewModel.BoardCells, cell => cell.Row == row && cell.Column == column);
}
