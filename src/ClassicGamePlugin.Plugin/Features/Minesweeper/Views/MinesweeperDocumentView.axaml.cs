using Avalonia.Controls;
using ClassicGamePlugin.Features.Minesweeper;

namespace ClassicGamePlugin.Features.Minesweeper.Views;

/// <summary>
/// Plugin SDK 专用的边界 View。Host 会把 <see cref="MinesweeperDocument"/> 设置为 DataContext，
/// 本 View 仅通过单向绑定把 Document 拥有的 ViewModel 交给真正的 <see cref="MinesweeperView"/>。
/// </summary>
public partial class MinesweeperDocumentView : UserControl
{
    /// <summary>创建 Document 包装 View 并加载唯一的 ViewModel 转接绑定。</summary>
    public MinesweeperDocumentView() => InitializeComponent();

    /// <summary>获取包装层当前承载的 ViewModel，仅用于验证 SDK 边界绑定。</summary>
    internal object? HostedViewModel => ViewModelHost.Content;
}
