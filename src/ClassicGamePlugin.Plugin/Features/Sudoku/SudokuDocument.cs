using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Sudoku.Domain;
using ClassicGamePlugin.Features.Sudoku.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Sudoku;

/// <summary>
/// 数独与 Plugin SDK 之间的窄生命周期适配器。Document 只处理 Host 标题、拥有每次打开时独立的 ViewModel，
/// 并在关闭时级联停止计时刷新和后台题目生成。
/// </summary>
public sealed class SudokuDocument : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private DocumentPresentationState _presentation = new("数独");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _disposed;

    public SudokuDocument()
        : this(new SudokuViewModel())
    {
    }

    internal SudokuDocument(
        ISudokuPuzzleProvider puzzleProvider,
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer)
        : this(new SudokuViewModel(puzzleProvider, timeProvider, enableDisplayRefreshTimer))
    {
    }

    private SudokuDocument(SudokuViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(
            this,
            (PluginIds.RestartSudoku, ViewModel.RestartCommand),
            (PluginIds.UndoSudoku, ViewModel.UndoCommand));
    }

    public SudokuViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    /// <summary>转发当前数独实例中精确到 CommandId 的工作台状态变化。</summary>
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

    /// <summary>幂等释放数独 ViewModel 拥有的计时刷新、动画订阅通知和生成取消令牌。</summary>
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
