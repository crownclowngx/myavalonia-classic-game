using Avalonia.Controls;

namespace ClassicGamePlugin.Features.Sudoku.Views;

/// <summary>
/// Plugin SDK 边界 View。Host 传入的是 SudokuDocument，本包装只通过单向绑定把其 ViewModel 交给游戏 View。
/// </summary>
public partial class SudokuDocumentView : UserControl
{
    public SudokuDocumentView() => InitializeComponent();

    internal object? HostedViewModel => ViewModelHost.Content;
}
