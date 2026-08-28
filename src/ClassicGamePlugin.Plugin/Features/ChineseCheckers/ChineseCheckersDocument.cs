using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.ChineseCheckers.Domain;
using ClassicGamePlugin.Features.ChineseCheckers.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.ChineseCheckers;

/// <summary>
/// 中国跳棋与 Plugin SDK 之间的窄适配器，只拥有 ViewModel、处理 Host 标题并级联释放电脑、计时和动画。
/// </summary>
public sealed class ChineseCheckersDocument : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private DocumentPresentationState _presentation = new("中国跳棋");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _disposed;

    public ChineseCheckersDocument()
        : this(new ChineseCheckersViewModel())
    {
    }

    internal ChineseCheckersDocument(
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer,
        IReadOnlyDictionary<ChineseCheckersAiDifficulty, IChineseCheckersMoveStrategy> strategies)
        : this(new ChineseCheckersViewModel(timeProvider, enableDisplayRefreshTimer, strategies))
    {
    }

    private ChineseCheckersDocument(ChineseCheckersViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(
            this,
            (PluginIds.RestartChineseCheckers, ViewModel.RestartCommand),
            (PluginIds.UndoChineseCheckers, ViewModel.UndoCommand));
    }

    public ChineseCheckersViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    /// <summary>转发当前中国跳棋实例中精确到 CommandId 的工作台状态变化。</summary>
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
