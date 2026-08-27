using Avalonia.Controls;
using ClassicGamePlugin.Features.Tetris.ViewModels;

namespace ClassicGamePlugin.Features.Tetris.Views;

/// <summary>俄罗斯方块布局 View 只承载编译绑定；实时输入、计时与绘制由专用棋盘控件负责。</summary>
public partial class TetrisView : UserControl
{
    public TetrisView() => InitializeComponent();
    internal TetrisViewModel? HostedViewModel => DataContext as TetrisViewModel;
}
