using ClassicGamePlugin.Features.Tetris.ViewModels;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Tetris;

/// <summary>
/// 俄罗斯方块与 Plugin SDK 之间的窄适配器。每个 Document 拥有独立对局和动画偏好；游戏循环计时器属于视觉树中的
/// 自绘控件，离开视觉树即停止，因此 Document 不实现没有真实资源可释放的 <see cref="IDisposable"/>。
/// </summary>
public sealed class TetrisDocument : IPluginDocument
{
    private DocumentPresentationState _presentation = new("俄罗斯方块");

    public TetrisDocument()
        : this(new TetrisViewModel())
    {
    }

    internal TetrisDocument(TetrisViewModel viewModel) =>
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    public TetrisViewModel ViewModel { get; }
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

