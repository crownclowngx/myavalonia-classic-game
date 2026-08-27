using Avalonia.Controls;

namespace ClassicGamePlugin.Features.Xiangqi.Views;

/// <summary>Plugin SDK 边界 View，仅通过单向绑定把 Document 的 ViewModel 交给正式游戏 View。</summary>
public partial class XiangqiDocumentView : UserControl
{
    public XiangqiDocumentView() => InitializeComponent();

    internal object? HostedViewModel => ViewModelHost.Content;
}
