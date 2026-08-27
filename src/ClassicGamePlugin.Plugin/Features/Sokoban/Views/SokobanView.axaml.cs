using Avalonia.Controls;
using ClassicGamePlugin.Features.Sokoban.ViewModels;

namespace ClassicGamePlugin.Features.Sokoban.Views;

/// <summary>推箱子布局 View 只承载编译绑定；规则、输入映射和绘制分别由 ViewModel 与棋盘控件负责。</summary>
public partial class SokobanView : UserControl
{
    public SokobanView() => InitializeComponent();
    internal SokobanViewModel? HostedViewModel => DataContext as SokobanViewModel;
}
