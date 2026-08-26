using Avalonia.Controls;
using ClassicGamePlugin.Features.SpiderSolitaire.ViewModels;

namespace ClassicGamePlugin.Features.SpiderSolitaire.Views;

/// <summary>
/// 蜘蛛纸牌页面布局。真正的牌面绘制和物理输入位于 <see cref="SpiderBoardControl"/>，
/// 本 View 不感知 Plugin SDK Document，也不改写外部传入的 DataContext。
/// </summary>
public partial class SpiderSolitaireView : UserControl
{
    public SpiderSolitaireView() => InitializeComponent();

    internal SpiderSolitaireViewModel? HostedViewModel => DataContext as SpiderSolitaireViewModel;
}
