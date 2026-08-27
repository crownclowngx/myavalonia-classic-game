using Avalonia.Controls;

namespace ClassicGamePlugin.Features.Tetris.Views;

/// <summary>Plugin SDK 边界包装，仅通过单向编译绑定把 Document 的独立 ViewModel 交给正式游戏 View。</summary>
public partial class TetrisDocumentView : UserControl
{
    public TetrisDocumentView() => InitializeComponent();
    internal object? HostedViewModel => ViewModelHost.Content;
}

