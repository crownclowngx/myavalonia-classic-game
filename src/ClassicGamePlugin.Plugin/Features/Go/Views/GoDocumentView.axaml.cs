using Avalonia.Controls;

namespace ClassicGamePlugin.Features.Go.Views;

/// <summary>Plugin SDK 边界 View，仅通过单向绑定把 Document 拥有的 ViewModel 交给正式游戏 View。</summary>
public partial class GoDocumentView : UserControl
{
    public GoDocumentView() => InitializeComponent();

    internal object? HostedViewModel => ViewModelHost.Content;
}
