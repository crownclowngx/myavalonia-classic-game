using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Go.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Go;

/// <summary>
/// 围棋与 Plugin SDK 之间的窄适配器。Document 只拥有页面 ViewModel、处理 Host 标题与释放生命周期，
/// 不承载落子、全局同形、数子或动画规则。
/// </summary>
public sealed class GoDocument : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private DocumentPresentationState _presentation = new("围棋");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _disposed;

    public GoDocument()
        : this(new GoViewModel())
    {
    }

    internal GoDocument(TimeProvider timeProvider, bool enableDisplayRefreshTimer)
        : this(new GoViewModel(timeProvider, enableDisplayRefreshTimer))
    {
    }

    private GoDocument(GoViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(
            this,
            (PluginIds.RestartGo, ViewModel.RestartCommand),
            (PluginIds.UndoGo, ViewModel.UndoCommand));
    }

    public GoViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    /// <summary>转发当前围棋实例中精确到 CommandId 的工作台状态变化。</summary>
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

    /// <summary>采用 Host 提供的非空标题初始化 Document；空白标题保留“围棋”。</summary>
    public ValueTask InitializeAsync(DocumentActivation activation, CancellationToken cancellationToken)
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

    /// <summary>级联停止 ViewModel 拥有的显示刷新、累计计时和动画输入锁。</summary>
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
