using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Xiangqi.Domain;
using ClassicGamePlugin.Features.Xiangqi.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Xiangqi;

/// <summary>
/// 中国象棋与 Plugin SDK 之间的窄适配器。Document 只拥有页面 ViewModel、处理 Host 标题并级联释放，
/// 不承载棋盘规则、电脑搜索或玩家命令。
/// </summary>
public sealed class XiangqiDocument : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private DocumentPresentationState _presentation = new("中国象棋");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _disposed;

    public XiangqiDocument()
        : this(new XiangqiViewModel())
    {
    }

    internal XiangqiDocument(
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer,
        IReadOnlyDictionary<XiangqiAiDifficulty, IXiangqiMoveStrategy> computerStrategies)
        : this(new XiangqiViewModel(timeProvider, enableDisplayRefreshTimer, computerStrategies))
    {
    }

    private XiangqiDocument(XiangqiViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(
            this,
            (PluginIds.RestartXiangqi, ViewModel.RestartCommand),
            (PluginIds.UndoXiangqi, ViewModel.UndoCommand));
    }

    public XiangqiViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    /// <summary>转发当前中国象棋实例中精确到 CommandId 的工作台状态变化。</summary>
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
