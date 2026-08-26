using ClassicGamePlugin.Features.SpiderSolitaire.Domain;
using ClassicGamePlugin.Features.SpiderSolitaire.ViewModels;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.SpiderSolitaire;

/// <summary>
/// 蜘蛛纸牌与 Plugin SDK 之间的窄适配器。Document 只拥有页面 ViewModel、处理 Host 标题和释放生命周期，
/// 不暴露牌列规则、命令或绘制状态。
/// </summary>
public sealed class SpiderSolitaireDocument : IPluginDocument, IDisposable
{
    private DocumentPresentationState _presentation = new("蜘蛛纸牌");
    private bool _disposed;

    public SpiderSolitaireDocument()
        : this(new SpiderSolitaireViewModel())
    {
    }

    internal SpiderSolitaireDocument(
        ISpiderCardShuffler shuffler,
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer)
        : this(new SpiderSolitaireViewModel(shuffler, timeProvider, enableDisplayRefreshTimer))
    {
    }

    private SpiderSolitaireDocument(SpiderSolitaireViewModel viewModel) =>
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    public SpiderSolitaireViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

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
