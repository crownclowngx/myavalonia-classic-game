using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.ChineseCheckers;
using ClassicGamePlugin.Features.FreeCell;
using ClassicGamePlugin.Features.Game2048;
using ClassicGamePlugin.Features.Go;
using ClassicGamePlugin.Features.Gomoku;
using ClassicGamePlugin.Features.Match3;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.Reversi;
using ClassicGamePlugin.Features.Sokoban;
using ClassicGamePlugin.Features.SpiderSolitaire;
using ClassicGamePlugin.Features.Sudoku;
using ClassicGamePlugin.Features.Tetris;
using ClassicGamePlugin.Features.Xiangqi;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

/// <summary>
/// 验证所有 ClassicGame Document 的工作台边界。具体棋规继续由各游戏既有测试负责，
/// 本组只证明 CommandId 没有接错实例、状态事件保持定向并且释放后统一 fail closed。
/// </summary>
public sealed class WorkbenchDocumentCommandTests
{
    [Theory]
    [MemberData(nameof(AllGames))]
    public async Task 十三个游戏均暴露已有Restart且不会伪造不存在的Undo(
        Func<IPluginDocument> factory,
        CommandId restartCommandId,
        CommandId? undoCommandId)
    {
        var document = factory();
        var disposable = Assert.IsAssignableFrom<IDisposable>(document);
        var target = Assert.IsAssignableFrom<IWorkbenchDocumentCommandTarget>(document);
        EventHandler<WorkbenchCommandStateChangedEventArgs> ignored = (_, _) => { };
        target.CommandStateChanged += ignored;
        target.CommandStateChanged -= ignored;
        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);

        Assert.True(target.CanExecute(restartCommandId));
        await target.ExecuteAsync(restartCommandId, CancellationToken.None);

        if (undoCommandId is not null)
        {
            Assert.False(target.CanExecute(undoCommandId));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.ExecuteAsync(undoCommandId, CancellationToken.None).AsTask());
        }

        var unknown = new CommandId("myavalonia.plugin.classic.game.command.unknown.restart");
        Assert.False(target.CanExecute(unknown));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => target.ExecuteAsync(unknown, CancellationToken.None).AsTask());

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => target.ExecuteAsync(restartCommandId, cancellation.Token).AsTask());

        disposable.Dispose();
        disposable.Dispose();
        Assert.False(target.CanExecute(restartCommandId));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.ExecuteAsync(restartCommandId, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => document.InitializeAsync(
                new NewDocumentActivation(string.Empty),
                CancellationToken.None).AsTask());
    }

    [Theory]
    [MemberData(nameof(GamesWithUndo))]
    public void 九个Undo状态通知均携带准确身份且释放后完成退订(
        Func<IPluginDocument> factory,
        CommandId undoCommandId)
    {
        var document = factory();
        var disposable = Assert.IsAssignableFrom<IDisposable>(document);
        var target = Assert.IsAssignableFrom<IWorkbenchDocumentCommandTarget>(document);
        var notifications = new List<CommandId>();
        target.CommandStateChanged += (_, args) => notifications.Add(args.CommandId);

        NotifyLocalUndoCanExecuteChanged(document);

        Assert.Equal([undoCommandId], notifications);
        notifications.Clear();

        disposable.Dispose();
        Assert.Contains(undoCommandId, notifications);
        notifications.Clear();
        NotifyLocalUndoCanExecuteChanged(document);
        Assert.Empty(notifications);
    }

    public static TheoryData<Func<IPluginDocument>, CommandId, CommandId?> AllGames => new()
    {
        { () => new MinesweeperDocument(), PluginIds.RestartMinesweeper, null },
        { () => new SpiderSolitaireDocument(), PluginIds.RestartSpiderSolitaire, PluginIds.UndoSpiderSolitaire },
        { () => new ReversiDocument(), PluginIds.RestartReversi, PluginIds.UndoReversi },
        { () => new GomokuDocument(), PluginIds.RestartGomoku, PluginIds.UndoGomoku },
        { () => new GoDocument(), PluginIds.RestartGo, PluginIds.UndoGo },
        { () => new XiangqiDocument(), PluginIds.RestartXiangqi, PluginIds.UndoXiangqi },
        { () => new Game2048Document(), PluginIds.RestartGame2048, null },
        { () => new SudokuDocument(), PluginIds.RestartSudoku, PluginIds.UndoSudoku },
        { () => new SokobanDocument(), PluginIds.RestartSokoban, PluginIds.UndoSokoban },
        { () => new TetrisDocument(), PluginIds.RestartTetris, null },
        { () => new FreeCellDocument(), PluginIds.RestartFreeCell, PluginIds.UndoFreeCell },
        { () => new Match3Document(), PluginIds.RestartMatch3, null },
        { () => new ChineseCheckersDocument(), PluginIds.RestartChineseCheckers, PluginIds.UndoChineseCheckers },
    };

    public static TheoryData<Func<IPluginDocument>, CommandId> GamesWithUndo => new()
    {
        { () => new SpiderSolitaireDocument(), PluginIds.UndoSpiderSolitaire },
        { () => new ReversiDocument(), PluginIds.UndoReversi },
        { () => new GomokuDocument(), PluginIds.UndoGomoku },
        { () => new GoDocument(), PluginIds.UndoGo },
        { () => new XiangqiDocument(), PluginIds.UndoXiangqi },
        { () => new SudokuDocument(), PluginIds.UndoSudoku },
        { () => new SokobanDocument(), PluginIds.UndoSokoban },
        { () => new FreeCellDocument(), PluginIds.UndoFreeCell },
        { () => new ChineseCheckersDocument(), PluginIds.UndoChineseCheckers },
    };

    /// <summary>
    /// 测试只借助公开 ViewModel/Command 形状触发已有 RelayCommand 状态事件，不读取适配器内部字段；
    /// 这样可以同时验证每个 Document 是否确实订阅了自己的 UndoCommand，而非共享或串线的命令。
    /// </summary>
    private static void NotifyLocalUndoCanExecuteChanged(IPluginDocument document)
    {
        var viewModel = document.GetType().GetProperty("ViewModel")!.GetValue(document)!;
        var undoCommand = viewModel.GetType().GetProperty("UndoCommand")!.GetValue(viewModel)!;
        undoCommand.GetType().GetMethod("NotifyCanExecuteChanged")!.Invoke(undoCommand, null);
    }
}
