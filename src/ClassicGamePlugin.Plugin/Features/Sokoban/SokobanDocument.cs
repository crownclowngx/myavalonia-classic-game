using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Sokoban.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Sokoban;

/// <summary>
/// 推箱子与 Plugin SDK 之间的窄生命周期适配器。Document 只拥有每次打开时独立的 ViewModel 和 Host 标题；
/// 动画计时器属于视觉树中的棋盘控件；Document 的释放职责仅是解除工作台命令状态订阅。
/// </summary>
public sealed class SokobanDocument : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private DocumentPresentationState _presentation = new("推箱子");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _disposed;

    public SokobanDocument()
        : this(new SokobanViewModel())
    {
    }

    internal SokobanDocument(SokobanViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(
            this,
            (PluginIds.RestartSokoban, ViewModel.RestartCommand),
            (PluginIds.UndoSokoban, ViewModel.UndoCommand));
    }

    public SokobanViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    /// <summary>转发当前推箱子实例中精确到 CommandId 的工作台状态变化。</summary>
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

    /// <summary>幂等释放工作台命令订阅；棋盘控件继续拥有并释放其动画计时器。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workbenchCommands.Dispose();
    }
}
