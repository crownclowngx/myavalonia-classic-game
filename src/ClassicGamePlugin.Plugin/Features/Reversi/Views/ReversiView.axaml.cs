using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassicGamePlugin.Features.Reversi.ViewModels;

namespace ClassicGamePlugin.Features.Reversi.Views;

/// <summary>
/// 黑白棋游戏 View 只负责布局和把格子点击转成 ViewModel 意图，
/// 不感知 Plugin SDK Document，也不在代码隐藏中重复合法落子规则。
/// </summary>
public partial class ReversiView : UserControl
{
    public ReversiView() => InitializeComponent();

    internal ReversiViewModel? HostedViewModel => DataContext as ReversiViewModel;

    private void OnCellClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { DataContext: ReversiCellViewModel cell })
        {
            HandleCellClick(cell);
            eventArgs.Handled = true;
        }
    }

    /// <summary>统一的点击转发入口，供真实 Button 和组合测试共同使用。</summary>
    internal void HandleCellClick(ReversiCellViewModel cell) => HostedViewModel?.PlayCell(cell);
}
