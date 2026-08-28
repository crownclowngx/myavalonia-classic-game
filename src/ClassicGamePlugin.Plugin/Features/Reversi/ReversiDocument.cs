using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Reversi.Domain;
using ClassicGamePlugin.Features.Reversi.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Reversi;

/// <summary>
/// 黑白棋与 Plugin SDK 之间的窄适配器。Document 只拥有页面 ViewModel、处理 Host 标题并释放生命周期，
/// 不承载棋盘、电脑策略或玩家命令。
/// </summary>
public sealed class ReversiDocument : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private DocumentPresentationState _presentation = new("黑白棋");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _disposed;

    public ReversiDocument()
        : this(new ReversiViewModel())
    {
    }

    internal ReversiDocument(
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer,
        IReadOnlyDictionary<ReversiAiDifficulty, IReversiMoveStrategy> computerStrategies)
        : this(new ReversiViewModel(timeProvider, enableDisplayRefreshTimer, computerStrategies))
    {
    }

    private ReversiDocument(ReversiViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(
            this,
            (PluginIds.RestartReversi, ViewModel.RestartCommand),
            (PluginIds.UndoReversi, ViewModel.UndoCommand));
    }

    public ReversiViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    /// <summary>转发当前黑白棋实例中精确到 CommandId 的工作台状态变化。</summary>
    public event EventHandler<WorkbenchCommandStateChangedEventArgs>? CommandStateChanged
    {
        add => _workbenchCommands.CommandStateChanged += value;
        remove => _workbenchCommands.CommandStateChanged -= value;
    }

    bool IWorkbenchDocumentCommandTarget.CanExecute(CommandId commandId) =>
        _workbenchCommands.CanExecute(commandId);

    ValueTask IWorkbenchDocumentCommandTarget.ExecuteAsync(
        CommandId commandId,
        CancellationToken cancellationToken) =>
        _workbenchCommands.ExecuteAsync(commandId, cancellationToken);

    /// <summary>采用 Host 提供的非空标题初始化 Document；空白标题保留“黑白棋”。</summary>
    public ValueTask InitializeAsync(
        DocumentActivation activation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(activation);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(activation.Title) &&
            !string.Equals(_presentation.Title, activation.Title, StringComparison.Ordinal))
        {
            _presentation = new DocumentPresentationState(activation.Title);
            PresentationChanged?.Invoke(this, EventArgs.Empty);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>级联停止 ViewModel 拥有的电脑任务、刷新计时器和游戏计时。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workbenchCommands.Dispose();
        ViewModel.Dispose();
    }
}
