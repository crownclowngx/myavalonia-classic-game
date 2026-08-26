using Avalonia.Controls;
using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.Minesweeper.Domain;
using ClassicGamePlugin.Features.Minesweeper.ViewModels;
using ClassicGamePlugin.Features.Minesweeper.Views;
using ClassicGamePlugin.Plugin;
using Microsoft.Extensions.DependencyInjection;
using MyAvaloniaManagement.PluginSdk;
using MyAvaloniaManagement.PluginSdk.UI;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class PluginCompositionTests
{
    [Fact]
    public void Module只注册一个普通扫雷Document()
    {
        var registration = new CapturingRegistration();

        new ClassicGamePluginModule().Configure(registration);

        Assert.NotNull(registration.DocumentDescriptor);
        Assert.Equal(PluginIds.MinesweeperDocument, registration.DocumentDescriptor.DocumentTypeId);
        Assert.Equal("扫雷", registration.DocumentDescriptor.DisplayName);
        Assert.Equal("经典游戏", registration.DocumentDescriptor.MenuCategory);
        Assert.Equal(typeof(MinesweeperDocument), registration.DocumentModel);
        Assert.Equal(typeof(MinesweeperDocumentView), registration.DocumentView);
        Assert.Null(registration.PersistableDocumentDescriptor);
    }

    [Fact]
    public void 稳定Plugin与扫雷Document身份保持冻结值()
    {
        Assert.Equal("myavalonia.plugin.classic.game", PluginIds.Plugin.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.minesweeper",
            PluginIds.MinesweeperDocument.Value);
    }

    [Fact]
    public void Document包装View通过单向绑定把ViewModel交给游戏View()
    {
        using var document = new MinesweeperDocument();
        var view = new MinesweeperDocumentView
        {
            DataContext = document,
        };

        Assert.Same(document, view.DataContext);
        Assert.Same(document.ViewModel, view.HostedViewModel);
    }

    [Fact]
    public void 游戏View直接接受ViewModel且不再改写DataContext()
    {
        using var document = new MinesweeperDocument();
        var view = new MinesweeperView
        {
            DataContext = document.ViewModel,
        };

        Assert.Same(document.ViewModel, view.DataContext);
    }

    [Fact]
    public void 游戏View把左键主要操作可靠转发给ViewModel()
    {
        using var viewModel = CreateInputTestViewModel();
        var view = new MinesweeperView { DataContext = viewModel };

        view.HandlePrimaryCellAction(FindCell(viewModel, 8, 8));

        Assert.Equal(MinesweeperGameState.Running, viewModel.GameState);
        Assert.True(FindCell(viewModel, 8, 8).IsRevealed);
    }

    [Fact]
    public void 游戏View支持纯右键插旗和数字格左右键组合展开()
    {
        using var viewModel = CreateInputTestViewModel();
        var view = new MinesweeperView { DataContext = viewModel };
        view.HandlePrimaryCellAction(FindCell(viewModel, 8, 8));

        var firstMine = FindCell(viewModel, 0, 0);
        var secondMine = FindCell(viewModel, 0, 2);
        view.HandlePointerButtons(firstMine, isLeftButtonPressed: false, isRightButtonPressed: true);
        view.HandlePointerButtons(secondMine, isLeftButtonPressed: false, isRightButtonPressed: true);
        Assert.Equal("⚑", firstMine.DisplayText);
        Assert.Equal("⚑", secondMine.DisplayText);

        var coveredSafeCell = FindCell(viewModel, 0, 1);
        Assert.False(coveredSafeCell.IsRevealed);

        // (1,1) 周围有 (0,0)、(0,2) 两颗雷；旗帜数匹配后，左右键组合应展开 (0,1)。
        var revealedNumber = FindCell(viewModel, 1, 1);
        Assert.True(revealedNumber.IsRevealed);
        view.HandlePointerButtons(
            revealedNumber,
            isLeftButtonPressed: true,
            isRightButtonPressed: true);

        Assert.True(coveredSafeCell.IsChordPreviewed);
        Assert.False(coveredSafeCell.IsRevealed);

        view.HandlePointerButtonsReleased(
            revealedNumber,
            isLeftButtonPressed: true,
            isRightButtonPressed: false);

        Assert.False(coveredSafeCell.IsChordPreviewed);
        Assert.True(coveredSafeCell.IsRevealed);
    }

    [Fact]
    public void 组合键预览只标记中心数字周围被覆盖且未插旗的格子()
    {
        using var viewModel = CreateInputTestViewModel();
        var view = new MinesweeperView { DataContext = viewModel };
        view.HandlePrimaryCellAction(FindCell(viewModel, 8, 8));
        var center = FindCell(viewModel, 1, 3);

        view.HandlePointerButtons(center, isLeftButtonPressed: true, isRightButtonPressed: true);

        var previewed = viewModel.BoardCells
            .Where(cell => cell.IsChordPreviewed)
            .Select(cell => (cell.Row, cell.Column))
            .ToArray();
        Assert.Equal([(0, 2), (0, 3), (0, 4), (1, 4)], previewed);

        view.CancelChordPreview();
        Assert.DoesNotContain(viewModel.BoardCells, cell => cell.IsChordPreviewed);
    }

    private static MinesweeperViewModel CreateInputTestViewModel()
    {
        var mines = Enumerable.Range(0, 5)
            .Select(index => new CellCoordinate(0, index * 2))
            .Concat(Enumerable.Range(4, 5).Select(column => new CellCoordinate(1, column)))
            .ToArray();
        return new MinesweeperViewModel(
            new FixedMinePlacementStrategy(mines),
            new ManualTimeProvider(),
            enableDisplayRefreshTimer: false);
    }

    private static MinesweeperCellViewModel FindCell(
        MinesweeperViewModel viewModel,
        int row,
        int column) =>
        Assert.Single(viewModel.BoardCells, cell => cell.Row == row && cell.Column == column);

    private sealed class CapturingRegistration : IPluginRegistration
    {
        public PluginId PluginId { get; } = PluginIds.Plugin;
        public IServiceCollection Services { get; } = new ServiceCollection();
        internal DocumentDescriptor? DocumentDescriptor { get; private set; }
        internal DocumentDescriptor? PersistableDocumentDescriptor { get; private set; }
        internal Type? DocumentModel { get; private set; }
        internal Type? DocumentView { get; private set; }

        public void UseLifecycle<TLifecycle>() where TLifecycle : class, IPluginLifecycle =>
            throw new NotSupportedException();

        public void AddDocument<TDocument, TView>(DocumentDescriptor descriptor)
            where TDocument : class, IPluginDocument
            where TView : Control, new()
        {
            DocumentDescriptor = descriptor;
            DocumentModel = typeof(TDocument);
            DocumentView = typeof(TView);
        }

        public void AddPersistableDocument<TDocument, TView>(DocumentDescriptor descriptor)
            where TDocument : class, IPersistablePluginDocument
            where TView : Control, new() => PersistableDocumentDescriptor = descriptor;

        public void AddTool<TTool, TView>(ToolDescriptor descriptor)
            where TTool : class
            where TView : Control, new() => throw new NotSupportedException();
    }
}
