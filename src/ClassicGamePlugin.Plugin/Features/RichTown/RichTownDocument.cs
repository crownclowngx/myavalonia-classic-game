using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.RichTown;

/// <summary>
/// 小镇独立子领域的 SDK 入口。G1 仅提供集成原型，不声明尚未实现的存档、重新开始或撤销能力。
/// Document 拥有当前视口的最终释放；临时从视觉树解绑由视口自行处理，不能误判为关闭对局。
/// </summary>
public sealed class RichTownDocument : IPluginDocument, IDisposable
{
    public static DocumentTypeId TypeId { get; } = new("myavalonia.plugin.classic.game.document.rich-town");
    private DocumentPresentationState _presentation = new("富翁小镇 3D（原型）");
    private IDisposable? _surface;
    private bool _disposed;
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

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

    /// <summary>替换 View 时释放旧表面；关闭后到达的绑定立即释放，不允许重新打开 GPU。</summary>
    internal void AttachSurface(IDisposable surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (ReferenceEquals(surface, _surface)) return;
        if (_disposed) { surface.Dispose(); return; }
        _surface?.Dispose();
        _surface = surface;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _surface?.Dispose();
        _surface = null;
        PresentationChanged = null;
    }
}
