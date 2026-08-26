using ClassicGamePlugin.Features.Minesweeper.Domain;
using ClassicGamePlugin.Features.Minesweeper.ViewModels;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Minesweeper;

/// <summary>
/// 扫雷与 Plugin SDK 之间的窄适配器。Document 只管理 Host 标签、初始化和释放生命周期，
/// 所有界面状态与玩家操作均委托给独立的 <see cref="MinesweeperViewModel"/>。
/// </summary>
public sealed class MinesweeperDocument : IPluginDocument, IDisposable
{
    private DocumentPresentationState _presentation = new("扫雷");
    private bool _disposed;

    /// <summary>创建供 Host 或 Standalone 使用的 Document，并组合生产环境 ViewModel。</summary>
    public MinesweeperDocument()
        : this(new MinesweeperViewModel())
    {
    }

    /// <summary>使用可测试依赖创建 Document，避免测试通过真实随机数或墙钟间接验证 ViewModel。</summary>
    internal MinesweeperDocument(
        IMinePlacementStrategy minePlacementStrategy,
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer)
        : this(new MinesweeperViewModel(
            minePlacementStrategy,
            timeProvider,
            enableDisplayRefreshTimer))
    {
    }

    private MinesweeperDocument(MinesweeperViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>获取由 View 使用的独立扫雷展示模型。</summary>
    public MinesweeperViewModel ViewModel { get; }

    /// <summary>获取 Host 当前应展示的标签标题。</summary>
    public DocumentPresentationState Presentation => _presentation;

    /// <summary>当 Host 标签展示信息变化时发出通知。</summary>
    public event EventHandler? PresentationChanged;

    /// <summary>使用 Host 已验证的标题初始化 Document；空白标题保留“扫雷”默认值。</summary>
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

    /// <summary>释放 ViewModel 拥有的 UI 刷新计时器及游戏计时状态。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ViewModel.Dispose();
    }
}
