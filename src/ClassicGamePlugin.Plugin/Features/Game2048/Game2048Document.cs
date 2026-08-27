using ClassicGamePlugin.Features.Game2048.Domain;
using ClassicGamePlugin.Features.Game2048.ViewModels;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Game2048;

/// <summary>
/// 2048 与 Plugin SDK 之间的窄生命周期适配器。Document 只接收 Host 标题并持有每次打开时独立创建的 ViewModel；
/// 本游戏没有计时器、后台任务或事件订阅，因此不实现没有实际资源可释放的 IDisposable。
/// </summary>
public sealed class Game2048Document : IPluginDocument
{
    private DocumentPresentationState _presentation = new("2048");

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
    }

    /// <summary>获取当前 Document 独占的 2048 展示模型。</summary>
    public Game2048ViewModel ViewModel { get; }

    /// <summary>获取 Host 标签应展示的标题。</summary>
    public DocumentPresentationState Presentation => _presentation;

    /// <summary>当 Host 标签展示信息变化时发出通知。</summary>
    public event EventHandler? PresentationChanged;

    /// <summary>使用 Host 已验证的非空标题初始化 Document；空白标题保留默认“2048”。</summary>
    public ValueTask InitializeAsync(
        DocumentActivation activation,
        CancellationToken cancellationToken)
    {
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
}
