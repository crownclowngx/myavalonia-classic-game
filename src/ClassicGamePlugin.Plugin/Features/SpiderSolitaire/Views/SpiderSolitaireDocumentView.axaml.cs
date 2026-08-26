using Avalonia.Controls;

namespace ClassicGamePlugin.Features.SpiderSolitaire.Views;

/// <summary>
/// Plugin SDK 边界 View。Host 设置的 DataContext 是 Document，本 View 只用单向绑定
/// 把 Document 拥有的 ViewModel 交给真正的游戏 View。
/// </summary>
public partial class SpiderSolitaireDocumentView : UserControl
{
    public SpiderSolitaireDocumentView() => InitializeComponent();

    internal object? HostedViewModel => ViewModelHost.Content;
}
