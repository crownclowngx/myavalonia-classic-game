using Avalonia.Controls;

namespace ClassicGamePlugin.Features.Match3.Views;

/// <summary>Plugin SDK 包装层，只通过单向绑定把 Match3Document 的 ViewModel 交给正式游戏 View。</summary>
public partial class Match3DocumentView : UserControl
{
    public Match3DocumentView() => InitializeComponent();
    internal object? HostedViewModel => ViewModelHost.Content;
}
