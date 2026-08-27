using Avalonia.Controls;

namespace ClassicGamePlugin.Features.Sokoban.Views;

/// <summary>Plugin SDK 边界包装，只通过单向绑定把 SokobanDocument 的 ViewModel 交给正式游戏 View。</summary>
public partial class SokobanDocumentView : UserControl
{
    public SokobanDocumentView() => InitializeComponent();
    internal object? HostedViewModel => ViewModelHost.Content;
}
