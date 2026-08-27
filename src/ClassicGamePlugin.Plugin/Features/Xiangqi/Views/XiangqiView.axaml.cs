using Avalonia.Controls;
using ClassicGamePlugin.Features.Xiangqi.ViewModels;

namespace ClassicGamePlugin.Features.Xiangqi.Views;

/// <summary>中国象棋 View 只组合布局；交叉点输入由专用棋盘控件转发，规则由 ViewModel/领域处理。</summary>
public partial class XiangqiView : UserControl
{
    public XiangqiView() => InitializeComponent();

    internal XiangqiViewModel? HostedViewModel => DataContext as XiangqiViewModel;
}
