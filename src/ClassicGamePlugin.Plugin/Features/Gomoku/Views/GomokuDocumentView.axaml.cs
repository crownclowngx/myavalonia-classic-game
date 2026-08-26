using Avalonia.Controls;

namespace ClassicGamePlugin.Features.Gomoku.Views;

/// <summary>Plugin SDK 边界 View，仅通过单向绑定把 Document 拥有的 ViewModel 交给游戏 View。</summary>
public partial class GomokuDocumentView : UserControl
{
    public GomokuDocumentView() => InitializeComponent();

    internal object? HostedViewModel => ViewModelHost.Content;
}
