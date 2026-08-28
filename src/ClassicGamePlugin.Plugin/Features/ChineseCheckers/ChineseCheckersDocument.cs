using ClassicGamePlugin.Features.ChineseCheckers.Domain;
using ClassicGamePlugin.Features.ChineseCheckers.ViewModels;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.ChineseCheckers;

/// <summary>
/// 中国跳棋与 Plugin SDK 之间的窄适配器，只拥有 ViewModel、处理 Host 标题并级联释放电脑、计时和动画。
/// </summary>
public sealed class ChineseCheckersDocument : IPluginDocument, IDisposable
{
    private DocumentPresentationState _presentation = new("中国跳棋");
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

    private ChineseCheckersDocument(ChineseCheckersViewModel viewModel) =>
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    public ChineseCheckersViewModel ViewModel { get; }
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
