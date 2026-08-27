using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassicGamePlugin.Features.Sudoku.ViewModels;

namespace ClassicGamePlugin.Features.Sudoku.Views;

/// <summary>数独页面只负责组合棋盘与按钮；数字按钮复用 BoardControl 使用的同一个 ViewModel 命令。</summary>
public partial class SudokuView : UserControl
{
    public SudokuView() => InitializeComponent();

    internal SudokuViewModel? HostedViewModel => DataContext as SudokuViewModel;

    private void OnNumberButtonClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string text } && int.TryParse(text, out var number))
        {
            HostedViewModel?.EnterNumberCommand.Execute(number);
        }
    }
}
