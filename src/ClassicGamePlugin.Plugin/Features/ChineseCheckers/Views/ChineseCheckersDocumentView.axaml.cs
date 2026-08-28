using Avalonia.Controls;

namespace ClassicGamePlugin.Features.ChineseCheckers.Views;

/// <summary>Plugin SDK 边界 View，只把 Document 拥有的 ViewModel 单向交给正式游戏 View。</summary>
public partial class ChineseCheckersDocumentView : UserControl
{
    public ChineseCheckersDocumentView() => InitializeComponent();

    internal object? HostedViewModel => ViewModelHost.Content;
}
