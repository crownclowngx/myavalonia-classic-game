using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
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
using ClassicGamePlugin.Features.Xiangqi;
using ClassicGamePlugin.Features.Xiangqi.Views;
using ClassicGamePlugin.Features.Game2048;
using ClassicGamePlugin.Features.Game2048.Domain;
using ClassicGamePlugin.Features.Game2048.ViewModels;
using ClassicGamePlugin.Features.Game2048.Views;
using ClassicGamePlugin.Features.Sudoku;
using ClassicGamePlugin.Features.Sudoku.ViewModels;
using ClassicGamePlugin.Features.Sudoku.Views;
using ClassicGamePlugin.Features.Sokoban;
using ClassicGamePlugin.Features.Sokoban.ViewModels;
using ClassicGamePlugin.Features.Sokoban.Views;
using ClassicGamePlugin.Features.Tetris;
using ClassicGamePlugin.Features.Tetris.ViewModels;
using ClassicGamePlugin.Features.Tetris.Views;
using ClassicGamePlugin.Features.FreeCell;
using ClassicGamePlugin.Features.FreeCell.ViewModels;
using ClassicGamePlugin.Features.FreeCell.Views;
using ClassicGamePlugin.Plugin;
using Microsoft.Extensions.DependencyInjection;
using MyAvaloniaManagement.PluginSdk;
using MyAvaloniaManagement.PluginSdk.UI;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class PluginCompositionTests
{
    [Fact]
    public void Module注册十个独立游戏的普通Document()
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
            },
            xiangqi =>
            {
                Assert.Equal(PluginIds.XiangqiDocument, xiangqi.Descriptor.DocumentTypeId);
                Assert.Equal("中国象棋", xiangqi.Descriptor.DisplayName);
                Assert.Equal("经典游戏", xiangqi.Descriptor.MenuCategory);
                Assert.Equal(typeof(XiangqiDocument), xiangqi.Model);
                Assert.Equal(typeof(XiangqiDocumentView), xiangqi.View);
            },
            game2048 =>
            {
                Assert.Equal(PluginIds.Game2048Document, game2048.Descriptor.DocumentTypeId);
                Assert.Equal("2048", game2048.Descriptor.DisplayName);
                Assert.Equal(
                    "经典数字合并游戏：移动方块、合并同值数字并挑战 2048",
                    game2048.Descriptor.Description);
                Assert.Equal("经典游戏", game2048.Descriptor.MenuCategory);
                Assert.Equal(typeof(Game2048Document), game2048.Model);
                Assert.Equal(typeof(Game2048DocumentView), game2048.View);
            },
            sudoku =>
            {
                Assert.Equal(PluginIds.SudokuDocument, sudoku.Descriptor.DocumentTypeId);
                Assert.Equal("数独", sudoku.Descriptor.DisplayName);
                Assert.Equal(
                    "经典 9×9 数独：三级难度、候选笔记、提示与唯一解题目生成",
                    sudoku.Descriptor.Description);
                Assert.Equal("经典游戏", sudoku.Descriptor.MenuCategory);
                Assert.Equal(typeof(SudokuDocument), sudoku.Model);
                Assert.Equal(typeof(SudokuDocumentView), sudoku.View);
            },
            sokoban =>
            {
                Assert.Equal(PluginIds.SokobanDocument, sokoban.Descriptor.DocumentTypeId);
                Assert.Equal("推箱子", sokoban.Descriptor.DisplayName);
                Assert.Equal(
                    "经典推箱子：递进地图、键盘移动、不限次数撤销与轻量动画",
                    sokoban.Descriptor.Description);
                Assert.Equal("经典游戏", sokoban.Descriptor.MenuCategory);
                Assert.Equal(typeof(SokobanDocument), sokoban.Model);
                Assert.Equal(typeof(SokobanDocumentView), sokoban.View);
            },
            tetris =>
            {
                Assert.Equal(PluginIds.TetrisDocument, tetris.Descriptor.DocumentTypeId);
                Assert.Equal("俄罗斯方块", tetris.Descriptor.DisplayName);
                Assert.Equal(
                    "现代俄罗斯方块：SRS 旋转、暂存、幽灵块、完整计分与逐级加速",
                    tetris.Descriptor.Description);
                Assert.Equal("经典游戏", tetris.Descriptor.MenuCategory);
                Assert.Equal(typeof(TetrisDocument), tetris.Model);
                Assert.Equal(typeof(TetrisDocumentView), tetris.View);
            },
            freeCell =>
            {
                Assert.Equal(PluginIds.FreeCellDocument, freeCell.Descriptor.DocumentTypeId);
                Assert.Equal("空当接龙", freeCell.Descriptor.DisplayName);
                Assert.Equal(
                    "经典空当接龙：可解编号牌局、拖放纸牌、求解提示与安全自动收牌",
                    freeCell.Descriptor.Description);
                Assert.Equal("经典游戏", freeCell.Descriptor.MenuCategory);
                Assert.Equal(typeof(FreeCellDocument), freeCell.Model);
                Assert.Equal(typeof(FreeCellDocumentView), freeCell.View);
            });
        Assert.Empty(registration.PersistableDocuments);
    }

    [Fact]
    public void 稳定Plugin与十个Document身份保持冻结值()
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
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.xiangqi",
            PluginIds.XiangqiDocument.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.2048",
            PluginIds.Game2048Document.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.sudoku",
            PluginIds.SudokuDocument.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.sokoban",
            PluginIds.SokobanDocument.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.tetris",
            PluginIds.TetrisDocument.Value);
        Assert.Equal(
            "myavalonia.plugin.classic.game.document.freecell",
            PluginIds.FreeCellDocument.Value);
    }

    [Fact]
    public void Document包装View通过单向绑定把ViewModel交给游戏View()
    {
        using var document = new MinesweeperDocument();
        var view = new MinesweeperDocumentView
        {
            DataContext = document,
        };

        AssertDocumentWrapperBinding(view, document);
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

        AssertDocumentWrapperBinding(wrapper, document);
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

        AssertDocumentWrapperBinding(wrapper, document);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
        Assert.Equal(1, document.ViewModel.MoveCount);
    }

    [Fact]
    public void 五子棋包装View通过单向绑定把独立ViewModel交给游戏View()
    {
        using var document = new GomokuDocument();
        var wrapper = new GomokuDocumentView { DataContext = document };
        var gameView = new GomokuView { DataContext = document.ViewModel };

        AssertDocumentWrapperBinding(wrapper, document);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
    }

    [Fact]
    public void 中国象棋包装View通过单向绑定把独立ViewModel交给游戏View()
    {
        using var document = new XiangqiDocument();
        var wrapper = new XiangqiDocumentView { DataContext = document };
        var gameView = new XiangqiView { DataContext = document.ViewModel };

        AssertDocumentWrapperBinding(wrapper, document);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
    }

    [Fact]
    public void 二零四八包装View通过单向绑定且游戏View只接受ViewModel()
    {
        var strategy = new FirstEmptyTileSpawnStrategy(2, 4, 2);
        var document = new Game2048Document(strategy);
        var wrapper = new Game2048DocumentView { DataContext = document };
        var gameView = new Game2048View { DataContext = document.ViewModel };

        AssertDocumentWrapperBinding(wrapper, document);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
        Assert.IsType<Game2048ViewModel>(gameView.DataContext);
        Assert.True(document.ViewModel.AnimationsEnabled);
        // 无窗口 Avalonia 测试不启动 DispatcherTimer；动画计划和取消生命周期由纯状态机测试覆盖。
        document.ViewModel.AnimationsEnabled = false;
        Assert.True(gameView.HandleKey(Key.Down));
        Assert.Equal(3, strategy.CallCount);
        Assert.False(document.ViewModel.IsAnimationRunning);
        Assert.False(gameView.HandleKey(Key.Enter));
    }

    [Fact]
    public void 数独包装View通过单向绑定且游戏View只接受ViewModel()
    {
        using var document = new SudokuDocument();
        var wrapper = new SudokuDocumentView { DataContext = document };
        var gameView = new SudokuView { DataContext = document.ViewModel };

        AssertDocumentWrapperBinding(wrapper, document);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
        Assert.IsType<SudokuViewModel>(gameView.DataContext);
        Assert.True(document.ViewModel.AnimationsEnabled);
    }

    [Fact]
    public void 推箱子包装View通过单向绑定且游戏View只接受ViewModel()
    {
        var document = new SokobanDocument();
        var wrapper = new SokobanDocumentView { DataContext = document };
        var gameView = new SokobanView { DataContext = document.ViewModel };

        AssertDocumentWrapperBinding(wrapper, document);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
        Assert.IsType<SokobanViewModel>(gameView.DataContext);
        Assert.True(document.ViewModel.AnimationsEnabled);
    }

    [Fact]
    public void 俄罗斯方块包装View通过单向绑定且游戏View只接受ViewModel()
    {
        var document = new TetrisDocument();
        var wrapper = new TetrisDocumentView { DataContext = document };
        var gameView = new TetrisView { DataContext = document.ViewModel };

        AssertDocumentWrapperBinding(wrapper, document);
        Assert.Same(document.ViewModel, gameView.HostedViewModel);
        Assert.IsType<TetrisViewModel>(gameView.DataContext);
        Assert.True(document.ViewModel.AnimationsEnabled);
    }

    [Fact]
    public void 空当接龙包装View通过单向绑定且游戏View只接受ViewModel()
    {
        using var document = new FreeCellDocument();
        var wrapper = new FreeCellDocumentView { DataContext = document };
        var gameView = new FreeCellView { DataContext = document.ViewModel };

        AssertDocumentWrapperBinding(wrapper, document);
        Assert.Same(document.ViewModel, gameView.DataContext);
        Assert.IsType<FreeCellViewModel>(gameView.DataContext);
        Assert.True(document.ViewModel.AreAnimationsEnabled);
    }

    [Theory]
    [MemberData(nameof(Game2048MovementKeys))]
    public void 二零四八方向键与WASD映射为领域方向(Key key, int expectedDirection)
    {
        Assert.True(Game2048View.TryMapDirection(key, out var actual));
        Assert.Equal((Game2048Direction)expectedDirection, actual);
    }

    [Fact]
    public void 二零四八无关按键不会被游戏映射或吞掉()
    {
        Assert.False(Game2048View.TryMapDirection(Key.Enter, out _));
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

    /// <summary>
    /// 无窗口单元测试没有 Avalonia UI 消息循环，因此不等待绑定结果值；这里直接验证包装层的
    /// Content 属性已经安装编译绑定，同时验证 Host 传入的 Document 未被 View 改写。
    /// </summary>
    private static void AssertDocumentWrapperBinding(Control wrapper, object expectedDataContext)
    {
        Assert.Same(expectedDataContext, wrapper.DataContext);
        var host = Assert.IsType<ContentControl>(wrapper.FindControl<ContentControl>("ViewModelHost"));
        Assert.NotNull(BindingOperations.GetBindingExpressionBase(host, ContentControl.ContentProperty));
    }

    public static TheoryData<Key, int> Game2048MovementKeys => new()
    {
        { Key.Left, (int)Game2048Direction.Left },
        { Key.A, (int)Game2048Direction.Left },
        { Key.Right, (int)Game2048Direction.Right },
        { Key.D, (int)Game2048Direction.Right },
        { Key.Up, (int)Game2048Direction.Up },
        { Key.W, (int)Game2048Direction.Up },
        { Key.Down, (int)Game2048Direction.Down },
        { Key.S, (int)Game2048Direction.Down },
    };

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
