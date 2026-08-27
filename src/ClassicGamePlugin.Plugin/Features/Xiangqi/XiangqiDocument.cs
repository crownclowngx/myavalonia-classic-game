using ClassicGamePlugin.Features.Xiangqi.Domain;
using ClassicGamePlugin.Features.Xiangqi.ViewModels;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Xiangqi;

/// <summary>
/// 中国象棋与 Plugin SDK 之间的窄适配器。Document 只拥有页面 ViewModel、处理 Host 标题并级联释放，
/// 不承载棋盘规则、电脑搜索或玩家命令。
/// </summary>
public sealed class XiangqiDocument : IPluginDocument, IDisposable
{
    private DocumentPresentationState _presentation = new("中国象棋");
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

    private XiangqiDocument(XiangqiViewModel viewModel) =>
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    public XiangqiViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

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
        ViewModel.Dispose();
    }
}
