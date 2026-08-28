using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Tetris.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Tetris;

/// <summary>
/// 俄罗斯方块与 Plugin SDK 之间的窄适配器。每个 Document 拥有独立对局和动画偏好；游戏循环计时器属于视觉树中的
/// 自绘控件，离开视觉树即停止；Document 实现 IDisposable 只为成对释放工作台命令状态订阅。
/// </summary>
public sealed class TetrisDocument : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private DocumentPresentationState _presentation = new("俄罗斯方块");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _disposed;

    public TetrisDocument()
        : this(new TetrisViewModel())
    {
    }

    internal TetrisDocument(TetrisViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(this, (PluginIds.RestartTetris, ViewModel.RestartCommand));
    }

    public TetrisViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    /// <summary>转发当前俄罗斯方块实例中精确到 CommandId 的工作台状态变化。</summary>
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

    /// <summary>幂等释放工作台命令订阅；游戏循环仍由视觉树中的控件管理。</summary>
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

