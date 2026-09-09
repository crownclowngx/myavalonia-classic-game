using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.RubiksCube.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.RubiksCube;

/// <summary>
/// 三阶魔方的 SDK 窄适配器。每个 Document 独占 ViewModel、棋局、求解取消源和播放器；
/// 工作台只投影同步重置命令，教学播放和组级回退保留在页面内。
/// </summary>
public sealed class RubiksCubeDocument : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private readonly WorkbenchDocumentCommandAdapter _commands;
    private DocumentPresentationState _presentation = new("三阶魔方");
    private bool _disposed;

    public RubiksCubeDocument() : this(new RubiksCubeViewModel()) { }
    internal RubiksCubeDocument(RubiksCubeViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _commands = new WorkbenchDocumentCommandAdapter(this, (PluginIds.RestartRubiksCube, ViewModel.RestartCommand));
    }

    public RubiksCubeViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;
    public event EventHandler<WorkbenchCommandStateChangedEventArgs>? CommandStateChanged
    {
        add => _commands.CommandStateChanged += value;
        remove => _commands.CommandStateChanged -= value;
    }

    bool IWorkbenchDocumentCommandTarget.CanExecute(CommandId commandId) => _commands.CanExecute(commandId);
    ValueTask IWorkbenchDocumentCommandTarget.ExecuteAsync(CommandId commandId, CancellationToken cancellationToken) =>
        _commands.ExecuteAsync(commandId, cancellationToken);

    public ValueTask InitializeAsync(DocumentActivation activation, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(activation);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(activation.Title) && activation.Title != _presentation.Title)
        {
            _presentation = new DocumentPresentationState(activation.Title);
            PresentationChanged?.Invoke(this, EventArgs.Empty);
        }
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _commands.Dispose();
        ViewModel.Dispose();
    }
}
