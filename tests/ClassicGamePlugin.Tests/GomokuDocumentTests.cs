using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Gomoku;
using ClassicGamePlugin.Features.Gomoku.Domain;
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

    [Fact]
    public async Task Workbench命令严格路由当前实例并复用既有重新开始与撤销用例()
    {
        using var first = CreateDocument();
        using var second = CreateDocument();
        var firstTarget = Assert.IsAssignableFrom<IWorkbenchDocumentCommandTarget>(first);
        var secondTarget = Assert.IsAssignableFrom<IWorkbenchDocumentCommandTarget>(second);

        Assert.True(firstTarget.CanExecute(PluginIds.RestartGomoku));
        Assert.False(firstTarget.CanExecute(PluginIds.UndoGomoku));
        Assert.False(secondTarget.CanExecute(PluginIds.UndoGomoku));

        first.ViewModel.PlayPosition(new GomokuPosition(0, 0));

        Assert.True(firstTarget.CanExecute(PluginIds.UndoGomoku));
        Assert.False(secondTarget.CanExecute(PluginIds.UndoGomoku));
        Assert.Equal(1, first.ViewModel.MoveCount);
        Assert.Equal(0, second.ViewModel.MoveCount);

        await firstTarget.ExecuteAsync(PluginIds.UndoGomoku, CancellationToken.None);
        Assert.Equal(0, first.ViewModel.MoveCount);

        first.ViewModel.PlayPosition(new GomokuPosition(1, 1));
        second.ViewModel.PlayPosition(new GomokuPosition(2, 2));
        await firstTarget.ExecuteAsync(PluginIds.RestartGomoku, CancellationToken.None);

        Assert.Equal(0, first.ViewModel.MoveCount);
        Assert.Equal(1, second.ViewModel.MoveCount);
        Assert.False(firstTarget.CanExecute(PluginIds.UndoGomoku));
        Assert.True(secondTarget.CanExecute(PluginIds.UndoGomoku));
    }

    [Fact]
    public async Task Workbench命令拒绝未知身份禁用状态和预取消调用()
    {
        using var document = CreateDocument();
        var target = Assert.IsAssignableFrom<IWorkbenchDocumentCommandTarget>(document);
        var unknown = new CommandId("myavalonia.plugin.classic.game.command.gomoku.unknown");

        Assert.False(target.CanExecute(unknown));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => target.ExecuteAsync(unknown, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.ExecuteAsync(PluginIds.UndoGomoku, CancellationToken.None).AsTask());

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => target.ExecuteAsync(PluginIds.RestartGomoku, cancellation.Token).AsTask());
        Assert.Equal(0, document.ViewModel.MoveCount);
    }

    [Fact]
    public void Workbench撤销状态按具体身份通知且释放后成对退订()
    {
        var document = CreateDocument();
        var target = Assert.IsAssignableFrom<IWorkbenchDocumentCommandTarget>(document);
        var notifications = new List<CommandId>();
        target.CommandStateChanged += (_, args) => notifications.Add(args.CommandId);

        document.ViewModel.PlayPosition(new GomokuPosition(0, 0));
        Assert.Contains(PluginIds.UndoGomoku, notifications);
        Assert.DoesNotContain(PluginIds.RestartGomoku, notifications);

        notifications.Clear();
        document.Dispose();
        Assert.Equal(
            [PluginIds.RestartGomoku, PluginIds.UndoGomoku],
            notifications);
        Assert.False(target.CanExecute(PluginIds.RestartGomoku));
        Assert.False(target.CanExecute(PluginIds.UndoGomoku));

        notifications.Clear();
        document.ViewModel.UndoCommand.NotifyCanExecuteChanged();
        document.Dispose();
        Assert.Empty(notifications);
    }

    private static GomokuDocument CreateDocument() =>
        new(new ManualTimeProvider(), false, GomokuTestStrategies.CreateFirstLegal());
}
