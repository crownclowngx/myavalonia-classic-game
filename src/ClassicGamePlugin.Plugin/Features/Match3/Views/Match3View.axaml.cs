using Avalonia.Controls;
using ClassicGamePlugin.Features.Match3.ViewModels;

namespace ClassicGamePlugin.Features.Match3.Views;

/// <summary>消消乐布局 View 只承载编译绑定；绘制、输入和领域规则由各自对象负责。</summary>
public partial class Match3View : UserControl
{
    public Match3View() => InitializeComponent();
    internal Match3ViewModel? HostedViewModel => DataContext as Match3ViewModel;
}
