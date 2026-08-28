using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Game2048.Domain;
using ClassicGamePlugin.Features.Game2048.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Game2048;

/// <summary>
/// 2048 与 Plugin SDK 之间的窄生命周期适配器。Document 只接收 Host 标题并持有每次打开时独立创建的 ViewModel；
/// 本游戏没有后台任务；实现 IDisposable 仅用于成对释放工作台命令状态订阅。
/// </summary>
public sealed class Game2048Document : IPluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    private DocumentPresentationState _presentation = new("2048");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _disposed;

    /// <summary>创建供 Host 或 Standalone 使用的生产 Document。</summary>
    public Game2048Document()
        : this(new Game2048ViewModel())
    {
    }

    /// <summary>使用确定性生成策略创建可测试 Document。</summary>
    internal Game2048Document(ITileSpawnStrategy tileSpawnStrategy)
        : this(new Game2048ViewModel(tileSpawnStrategy))
    {
    }

    private Game2048Document(Game2048ViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(this, (PluginIds.RestartGame2048, ViewModel.RestartCommand));
    }

    /// <summary>获取当前 Document 独占的 2048 展示模型。</summary>
    public Game2048ViewModel ViewModel { get; }

    /// <summary>获取 Host 标签应展示的标题。</summary>
    public DocumentPresentationState Presentation => _presentation;

    /// <summary>当 Host 标签展示信息变化时发出通知。</summary>
    public event EventHandler? PresentationChanged;

    /// <summary>转发当前 2048 实例中精确到 CommandId 的工作台状态变化。</summary>
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

    /// <summary>使用 Host 已验证的非空标题初始化 Document；空白标题保留默认“2048”。</summary>
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

    /// <summary>幂等释放工作台命令订阅；ViewModel 本身没有可释放资源。</summary>
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
