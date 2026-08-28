using Avalonia.Controls;
using ClassicGamePlugin.Features.ChineseCheckers.ViewModels;

namespace ClassicGamePlugin.Features.ChineseCheckers.Views;

/// <summary>中国跳棋页面布局；指针输入由棋盘控件翻译后交给 ViewModel。</summary>
public partial class ChineseCheckersView : UserControl
{
    public ChineseCheckersView() => InitializeComponent();

    internal ChineseCheckersViewModel? HostedViewModel => DataContext as ChineseCheckersViewModel;
}
