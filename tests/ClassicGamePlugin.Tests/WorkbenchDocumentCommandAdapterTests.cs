using System.Windows.Input;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

/// <summary>直接覆盖内部组合 Adapter 的防御分支，避免把协议错误伪装成某一款游戏的规则问题。</summary>
public sealed class WorkbenchDocumentCommandAdapterTests
{
    private static readonly CommandId FirstId =
        new("myavalonia.plugin.classic.game.command.adapter.first");
    private static readonly CommandId SecondId =
        new("myavalonia.plugin.classic.game.command.adapter.second");

    [Fact]
    public void 构造函数拒绝空发送者空映射空成员和重复身份()
    {
        var command = new FakeCommand();

        Assert.Throws<ArgumentNullException>(() => new WorkbenchDocumentCommandAdapter(
            null!, (FirstId, command)));
        Assert.Throws<ArgumentNullException>(() => new WorkbenchDocumentCommandAdapter(
            this, null!));
        Assert.Throws<ArgumentException>(() => new WorkbenchDocumentCommandAdapter(this));
        Assert.Throws<ArgumentNullException>(() => new WorkbenchDocumentCommandAdapter(
            this, (null!, command)));
        Assert.Throws<ArgumentNullException>(() => new WorkbenchDocumentCommandAdapter(
            this, (FirstId, null!)));
        Assert.Throws<ArgumentException>(() => new WorkbenchDocumentCommandAdapter(
            this, (FirstId, command), (FirstId, command)));
    }

    [Fact]
    public async Task 查询执行取消未知与禁用状态均遵守统一契约()
    {
        var command = new FakeCommand { IsEnabled = true };
        using var adapter = new WorkbenchDocumentCommandAdapter(this, (FirstId, command));

        Assert.Throws<ArgumentNullException>(() => adapter.CanExecute(null!));
        Assert.True(adapter.CanExecute(FirstId));
        Assert.False(adapter.CanExecute(SecondId));
        await adapter.ExecuteAsync(FirstId, CancellationToken.None);
        Assert.Equal(1, command.ExecutionCount);

        command.IsEnabled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.ExecuteAsync(FirstId, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.ExecuteAsync(SecondId, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => adapter.ExecuteAsync(null!, CancellationToken.None).AsTask());

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => adapter.ExecuteAsync(FirstId, cancellation.Token).AsTask());
    }

    [Fact]
    public void 状态事件保持Document发送者定向身份并在释放后退订()
    {
        var first = new FakeCommand();
        var second = new FakeCommand();
        var owner = new object();
        var adapter = new WorkbenchDocumentCommandAdapter(
            owner,
            (FirstId, first),
            (SecondId, second));
        var notifications = new List<(object? Sender, CommandId CommandId)>();
        adapter.CommandStateChanged += (sender, args) =>
            notifications.Add((sender, args.CommandId));

        first.Notify(first);
        second.Notify(second);
        first.Notify(null);

        Assert.Equal([FirstId, SecondId], notifications.Select(item => item.CommandId));
        Assert.All(notifications, item => Assert.Same(owner, item.Sender));
        notifications.Clear();

        adapter.Dispose();
        Assert.Equal([FirstId, SecondId], notifications.Select(item => item.CommandId));
        Assert.False(adapter.CanExecute(FirstId));
        adapter.Dispose();
        notifications.Clear();
        first.Notify(first);
        Assert.Empty(notifications);
    }

    private sealed class FakeCommand : ICommand
    {
        internal bool IsEnabled { get; set; } = true;
        internal int ExecutionCount { get; private set; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => IsEnabled;
        public void Execute(object? parameter) => ExecutionCount++;
        internal void Notify(object? sender) => CanExecuteChanged?.Invoke(sender, EventArgs.Empty);
    }
}
