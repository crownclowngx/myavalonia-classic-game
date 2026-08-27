using Avalonia.Controls;
using ClassicGamePlugin.Features.Go.ViewModels;

namespace ClassicGamePlugin.Features.Go.Views;

/// <summary>围棋页面布局；所有行为通过编译绑定交给 ViewModel。</summary>
public partial class GoView : UserControl
{
    public GoView() => InitializeComponent();

    internal GoViewModel? HostedViewModel => DataContext as GoViewModel;
}
