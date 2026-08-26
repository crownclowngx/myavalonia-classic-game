using Avalonia.Controls;
using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.Minesweeper.Domain;
using ClassicGamePlugin.Features.Minesweeper.ViewModels;
using ClassicGamePlugin.Features.Minesweeper.Views;
using ClassicGamePlugin.Features.SpiderSolitaire;
using ClassicGamePlugin.Features.SpiderSolitaire.Views;
using ClassicGamePlugin.Features.Reversi;
using ClassicGamePlugin.Features.Reversi.ViewModels;
using ClassicGamePlugin.Features.Reversi.Views;
using ClassicGamePlugin.Features.Gomoku;
using ClassicGamePlugin.Features.Gomoku.Views;
using ClassicGamePlugin.Plugin;
using Microsoft.Extensions.DependencyInjection;
using MyAvaloniaManagement.PluginSdk;
using MyAvaloniaManagement.PluginSdk.UI;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class PluginCompositionTests
{
    [Fact]
    public void Module注册四个独立游戏的普通Document()
    {
        var registration = new CapturingRegistration();

        new ClassicGamePluginModule().Configure(registration);

        Assert.Collection(
            registration.Documents,
            minesweeper =>
            {
                Assert.Equal(PluginIds.MinesweeperDocument, minesweeper.Descriptor.DocumentTypeId);
                Assert.Equal("扫雷", minesweeper.Descriptor.DisplayName);
                Assert.Equal("经典游戏", minesweeper.Descriptor.MenuCategory);
                Assert.Equal(typeof(MinesweeperDocument), minesweeper.Model);
                Assert.Equal(typeof(MinesweeperDocumentView), minesweeper.View);
            },
            spider =>
            {
                Assert.Equal(PluginIds.SpiderSolitaireDocument, spider.Descriptor.DocumentTypeId);
                Assert.Equal("蜘蛛纸牌", spider.Descriptor.DisplayName);
                Assert.Equal("经典游戏", spider.Descriptor.MenuCategory);
                Assert.Equal(typeof(SpiderSolitaireDocument), spider.Model);
                Assert.Equal(typeof(SpiderSolitaireDocumentView), spider.View);
            },
            reversi =>
            {
                Assert.Equal(PluginIds.ReversiDocument, reversi.Descriptor.DocumentTypeId);
                Assert.Equal("黑白棋", reversi.Descriptor.DisplayName);
                Assert.Equal("经典游戏", reversi.Descriptor.MenuCategory);
                Assert.Equal(typeof(ReversiDocument), reversi.Model);
                Assert.Equal(typeof(ReversiDocumentView), reversi.View);
            },
            gomoku =>
            {
                Assert.Equal(PluginIds.GomokuDocument, gomoku.Descriptor.DocumentTypeId);
                Assert.Equal("五子棋", gomoku.Descriptor.DisplayName);
                Assert.Equal("经典游戏", gomoku.Descriptor.MenuCategory);
                Assert.Equal(typeof(GomokuDocument), gomoku.Model);
                Assert.Equal(typeof(GomokuDocumentView), gomoku.View);
            });
        Assert.Empty(registration.PersistableDocuments);
    }

    [Fact]
    public void 稳定Plugin与四个Document身份保持冻结值()
    {
        Assert.Equal("myavalonia.plugin.classic.game", PluginIds.Plugin.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.minesweeper",
            PluginIds.MinesweeperDocument.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.spider-solitaire",
            PluginIds.SpiderSolitaireDocument.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.reversi",
            PluginIds.ReversiDocument.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.gomoku",
            PluginIds.GomokuDocument.Value);
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
    public void 蜘蛛纸牌包装View通过单向绑定把ViewModel交给游戏View()
    {
        using var document = new SpiderSolitaireDocument();
        var wrapper = new SpiderSolitaireDocumentView { DataContext = document };
        var gameView = new SpiderSolitaireView { DataContext = document.ViewModel };

        Assert.Same(document.ViewModel, wrapper.HostedViewModel);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
    }

    [Fact]
    public void 黑白棋包装View通过单向绑定且游戏View只转发点击()
    {
        using var document = new ReversiDocument();
        var wrapper = new ReversiDocumentView { DataContext = document };
        var gameView = new ReversiView { DataContext = document.ViewModel };
        var move = Assert.Single(document.ViewModel.BoardCells, cell => cell.Row == 2 && cell.Column == 3);

        gameView.HandleCellClick(move);

        Assert.Same(document.ViewModel, wrapper.HostedViewModel);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
        Assert.Equal(1, document.ViewModel.MoveCount);
    }

    [Fact]
    public void 五子棋包装View通过单向绑定把独立ViewModel交给游戏View()
    {
        using var document = new GomokuDocument();
        var wrapper = new GomokuDocumentView { DataContext = document };
        var gameView = new GomokuView { DataContext = document.ViewModel };

        Assert.Same(document.ViewModel, wrapper.HostedViewModel);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
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
        internal List<(DocumentDescriptor Descriptor, Type Model, Type View)> Documents { get; } = [];
        internal List<(DocumentDescriptor Descriptor, Type Model, Type View)> PersistableDocuments { get; } = [];

        public void UseLifecycle<TLifecycle>() where TLifecycle : class, IPluginLifecycle =>
            throw new NotSupportedException();

        public void AddDocument<TDocument, TView>(DocumentDescriptor descriptor)
            where TDocument : class, IPluginDocument
            where TView : Control, new()
            => Documents.Add((descriptor, typeof(TDocument), typeof(TView)));

        public void AddPersistableDocument<TDocument, TView>(DocumentDescriptor descriptor)
            where TDocument : class, IPersistablePluginDocument
            where TView : Control, new() =>
            PersistableDocuments.Add((descriptor, typeof(TDocument), typeof(TView)));

        public void AddTool<TTool, TView>(ToolDescriptor descriptor)
            where TTool : class
            where TView : Control, new() => throw new NotSupportedException();
    }
}
