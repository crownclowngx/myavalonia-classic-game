using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassicGamePlugin.Features.Game2048.ViewModels;

/// <summary>
/// 把固定位置上的一个 2048 数值投影为文本、颜色、字号和无障碍说明。该类型只负责展示，
/// 不判断合并或移动，也不持有领域棋盘引用。
/// </summary>
public sealed class Game2048CellViewModel : ObservableObject
{
    private int _value;

    internal Game2048CellViewModel(int row, int column)
    {
        Row = row;
        Column = column;
    }

    /// <summary>获取格子的零基行号。</summary>
    public int Row { get; }

    /// <summary>获取格子的零基列号。</summary>
    public int Column { get; }

    /// <summary>获取方块数值；零表示空格。</summary>
    public int Value => _value;

    /// <summary>获取当前格子是否包含需要显示的方块。</summary>
    public bool HasValue => _value != 0;

    /// <summary>获取空格留白或方块数字文本。</summary>
    public string DisplayText => Game2048TileAppearance.GetDisplayText(_value);

    /// <summary>获取经典 2048 色板；超过 2048 的数字使用稳定的深色回退色。</summary>
    public Avalonia.Media.IBrush Background => Game2048TileAppearance.GetBackground(_value);

    /// <summary>低值浅色方块使用深色字，其余方块使用浅色字以保持对比度。</summary>
    public Avalonia.Media.IBrush Foreground => Game2048TileAppearance.GetForeground(_value);

    /// <summary>随着位数增加缩小字号，避免继续挑战阶段的大数字越过格子边界。</summary>
    public double FontSize => Game2048TileAppearance.GetFontSize(_value);

    /// <summary>获取屏幕阅读器和工具提示使用的行列及数值说明。</summary>
    public string AccessibleText => _value == 0
        ? $"第 {Row + 1} 行，第 {Column + 1} 列，空格"
        : $"第 {Row + 1} 行，第 {Column + 1} 列，数值 {_value}";

    /// <summary>从领域棋盘刷新当前格子的全部派生展示属性。</summary>
    internal void Refresh(int value)
    {
        if (_value == value)
        {
            return;
        }

        _value = value;
        OnPropertyChanged(string.Empty);
    }
}
