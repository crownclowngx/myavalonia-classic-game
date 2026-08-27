using ClassicGamePlugin.Features.Sokoban.ViewModels;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Sokoban;

/// <summary>
/// 推箱子与 Plugin SDK 之间的窄生命周期适配器。Document 只拥有每次打开时独立的 ViewModel 和 Host 标题；
/// 动画计时器属于视觉树中的棋盘控件，因此 Document 本身没有后台资源需要释放。
/// </summary>
public sealed class SokobanDocument : IPluginDocument
{
    private DocumentPresentationState _presentation = new("推箱子");

    public SokobanDocument()
        : this(new SokobanViewModel())
    {
    }

    internal SokobanDocument(SokobanViewModel viewModel) =>
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    public SokobanViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    public ValueTask InitializeAsync(DocumentActivation activation, CancellationToken cancellationToken)
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
