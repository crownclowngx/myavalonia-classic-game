using Avalonia.Controls;
using ClassicGamePlugin.Features.Gomoku.ViewModels;

namespace ClassicGamePlugin.Features.Gomoku.Views;

/// <summary>五子棋游戏 View 只负责组合布局；交叉点输入由棋盘控件转发，规则仍由 ViewModel/领域处理。</summary>
public partial class GomokuView : UserControl
{
    public GomokuView() => InitializeComponent();

    internal GomokuViewModel? HostedViewModel => DataContext as GomokuViewModel;
}
