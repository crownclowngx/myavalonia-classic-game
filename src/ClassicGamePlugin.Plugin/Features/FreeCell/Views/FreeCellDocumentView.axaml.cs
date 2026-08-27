using Avalonia.Controls;

namespace ClassicGamePlugin.Features.FreeCell.Views;

/// <summary>Host Document 与游戏 ViewModel 之间的单向绑定包装，不承载牌局规则。</summary>
public partial class FreeCellDocumentView : UserControl
{
    public FreeCellDocumentView() => InitializeComponent();
    internal object? HostedViewModel => ViewModelHost.Content;
}
