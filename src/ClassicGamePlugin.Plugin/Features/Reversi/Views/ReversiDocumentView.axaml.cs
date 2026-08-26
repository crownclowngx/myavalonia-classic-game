using Avalonia.Controls;

namespace ClassicGamePlugin.Features.Reversi.Views;

/// <summary>
/// Plugin SDK 边界 View。Host 提供的 DataContext 是 <see cref="ReversiDocument"/>，
/// 本 View 只通过单向绑定把 Document 拥有的 ViewModel 交给真正的游戏 View。
/// </summary>
public partial class ReversiDocumentView : UserControl
{
    public ReversiDocumentView() => InitializeComponent();

    internal object? HostedViewModel => ViewModelHost.Content;
}
