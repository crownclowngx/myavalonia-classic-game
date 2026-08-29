using System.Windows.Input;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Workbench;

/// <summary>
/// 把一个 Document 已有的同步 <see cref="ICommand"/> 朴素地适配为工作台命令目标。
/// </summary>
/// <remarks>
/// 该类型只负责命令身份分派、执行前防御和事件订阅，不了解任何游戏规则，也不拥有 ViewModel。
/// 每个 Document 仍负责选择哪些本地命令可以暴露、决定资源释放顺序，并把 Host 标题生命周期留在自身。
/// 这种组合方式避免 13 个 Document 重复实现同一套边界代码，同时没有引入服务定位器、反射或新的公共接口。
/// </remarks>
internal sealed class WorkbenchDocumentCommandAdapter : IDisposable
{
    private readonly IReadOnlyDictionary<CommandId, ICommand> _commands;
    private readonly object _eventSender;
    private bool _disposed;

    /// <summary>使用属于同一个 Document 实例的稳定命令身份和本地命令创建适配器。</summary>
    /// <param name="commands">非空、身份唯一且命令实例非空的映射。</param>
    internal WorkbenchDocumentCommandAdapter(
        object eventSender,
        params (CommandId CommandId, ICommand Command)[] commands)
    {
        _eventSender = eventSender ?? throw new ArgumentNullException(nameof(eventSender));
        ArgumentNullException.ThrowIfNull(commands);
        if (commands.Length == 0)
        {
            throw new ArgumentException("至少需要声明一条工作台命令。", nameof(commands));
        }

        var commandMap = new Dictionary<CommandId, ICommand>();
        foreach (var (commandId, command) in commands)
        {
            ArgumentNullException.ThrowIfNull(commandId);
            ArgumentNullException.ThrowIfNull(command);
            if (!commandMap.TryAdd(commandId, command))
            {
                throw new ArgumentException($"工作台命令身份重复：{commandId.Value}", nameof(commands));
            }

            // 每条本地命令分别订阅，事件处理器通过 sender 反查唯一身份，因此状态变化只通知
            // 对应 CommandId；不使用 null 或“全部刷新”哨兵扩大 Host 的刷新范围。
            command.CanExecuteChanged += OnCanExecuteChanged;
        }

        _commands = commandMap;
    }

    /// <summary>当某一条已适配命令的可执行状态变化时发生。</summary>
    internal event EventHandler<WorkbenchCommandStateChangedEventArgs>? CommandStateChanged;

    /// <summary>查询未释放适配器中的指定命令是否可执行。</summary>
    internal bool CanExecute(CommandId commandId)
    {
        ArgumentNullException.ThrowIfNull(commandId);
        return !_disposed &&
            _commands.TryGetValue(commandId, out var command) &&
            command.CanExecute(null);
    }

    /// <summary>执行前重新检查身份、取消和当前实例状态，再同步调用既有本地命令。</summary>
    internal ValueTask ExecuteAsync(CommandId commandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_commands.TryGetValue(commandId, out var command))
        {
            throw new ArgumentOutOfRangeException(
                nameof(commandId),
                commandId,
                "当前游戏 Document Target 不拥有该工作台命令。");
        }

        if (_disposed || !command.CanExecute(null))
        {
            // Host Executor 正常路径也会重查状态；这里保留 Target 内部的最终防线，防止快速
            // 重复按键、插件内部误调用或关闭期间的迟到调用修改已失效的游戏实例。
            throw new InvalidOperationException("当前游戏 Document 状态不允许执行该工作台命令。");
        }

        command.Execute(null);

        // 暴露的 Restart/Undo 都是已有同步 RelayCommand。取消仅在提交前生效；返回已完成
        // ValueTask 既让 Host 观察到完成，又避免 async void、Task.Run 和无意义的线程切换。
        return ValueTask.CompletedTask;
    }

    /// <summary>成对退订全部本地命令，并把每条命令定向通知为 fail closed。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var (commandId, command) in _commands)
        {
            command.CanExecuteChanged -= OnCanExecuteChanged;
            NotifyStateChanged(commandId);
        }
    }

    private void OnCanExecuteChanged(object? sender, EventArgs eventArgs)
    {
        if (_disposed || sender is not ICommand changedCommand)
        {
            return;
        }

        var commandId = _commands.Single(pair => ReferenceEquals(pair.Value, changedCommand)).Key;
        NotifyStateChanged(commandId);
    }

    private void NotifyStateChanged(CommandId commandId)
    {
        // Host 通过 sender 的引用相等验证迟到通知，事件发送者必须是实现 Target 的 Document，
        // 不能泄露这个内部组合对象；否则正确的状态事件会被 Host 按失效来源安全忽略。
        CommandStateChanged?.Invoke(
            _eventSender,
            new WorkbenchCommandStateChangedEventArgs(commandId));
    }
}
