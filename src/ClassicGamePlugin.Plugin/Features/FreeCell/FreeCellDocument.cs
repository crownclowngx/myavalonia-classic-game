using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.FreeCell.Domain;
using ClassicGamePlugin.Features.FreeCell.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.FreeCell;

/// <summary>
/// 空当接龙与 Plugin SDK 的窄生命周期适配器。首次初始化会等待编号 1 的可解牌局生成；关闭 Document
/// 会取消求解与生成任务并停止计时，多个 Document 不共享棋局或后台状态。
/// </summary>
public sealed class FreeCellDocument : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private DocumentPresentationState _presentation = new("空当接龙");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _initialized;
    private bool _disposed;

    public FreeCellDocument()
        : this(new FreeCellViewModel())
    {
    }

    internal FreeCellDocument(
        IFreeCellDealProvider dealProvider,
        IFreeCellSolver solver,
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer)
        : this(new FreeCellViewModel(dealProvider, solver, timeProvider, enableDisplayRefreshTimer))
    {
    }

    private FreeCellDocument(FreeCellViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(
            this,
            (PluginIds.RestartFreeCell, ViewModel.ReplaySameDealCommand),
            (PluginIds.UndoFreeCell, ViewModel.UndoCommand));
    }

    public FreeCellViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    /// <summary>转发当前空当接龙实例中精确到 CommandId 的工作台状态变化。</summary>
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

    public async ValueTask InitializeAsync(
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

        if (!_initialized)
        {
            await ViewModel.InitializeAsync(1, cancellationToken).ConfigureAwait(true);
            _initialized = true;
        }
    }

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
