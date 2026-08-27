using ClassicGamePlugin.Features.Match3.ViewModels;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Match3;

/// <summary>
/// 消消乐与 Plugin SDK 之间的窄适配器。Document 只拥有独立 ViewModel 与标题；动画计时器属于视觉树，
/// 因此这里不实现没有实际资源需要释放的 IDisposable。
/// </summary>
public sealed class Match3Document : IPluginDocument
{
    private DocumentPresentationState _presentation = new("消消乐");

    public Match3Document()
        : this(new Match3ViewModel())
    {
    }

    internal Match3Document(Match3ViewModel viewModel) =>
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    public Match3ViewModel ViewModel { get; }
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
