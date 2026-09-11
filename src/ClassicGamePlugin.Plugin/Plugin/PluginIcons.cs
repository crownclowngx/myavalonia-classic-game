using MyAvaloniaManagement.PluginSdk.UI;

namespace ClassicGamePlugin.Plugin;

/// <summary>本插件专属矢量。保持纯不可变数据，不保存 Host 服务、控件或主题画刷。</summary>
internal static class PluginIcons
{
    /// <summary>地雷与放射引线。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition Minesweeper { get; } = new(
        "M5,10a5,5 0 1 0 10,0a5,5 0 1 0 -10,0Z M6.7,8a1.3,1.3 0 1 0 2.6,0a1.3,1.3 0 1 0 -2.6,0Z M9,1h2v4h-2Z M9,15h2v4h-2Z M1,9h4v2h-4Z M15,9h4v2h-4Z M3,2l3,3l-1,1l-3,-3Z M14,5l3,-3l1,1l-3,3Z M2,17l3,-3l1,1l-3,3Z M14,15l1,-1l3,3l-1,1Z", 20, 20);

    /// <summary>叠放纸牌与黑桃。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition SpiderCards { get; } = new(
        "M1,1h11v14h-11Z M2.5,2.5h8.0v11.0h-8.0Z M6,5h13v14h-13Z M7.5,6.5h10.0v11.0h-10.0Z M12.5,8l-4,5a2,2 0 0 0 3,2l-1,2h4l-1,-2a2,2 0 0 0 3,-2Z", 20, 20);

    /// <summary>黑白双棋子。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition Reversi { get; } = new(
        "M2,7a5,5 0 1 0 10,0a5,5 0 1 0 -10,0Z M8,13a5,5 0 1 0 10,0a5,5 0 1 0 -10,0Z M10,13a3,3 0 1 0 6,0a3,3 0 1 0 -6,0Z", 20, 20);

    /// <summary>连续五子。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition Gomoku { get; } = new(
        "M0.3999999999999999,10a1.6,1.6 0 1 0 3.2,0a1.6,1.6 0 1 0 -3.2,0Z M4.4,10a1.6,1.6 0 1 0 3.2,0a1.6,1.6 0 1 0 -3.2,0Z M8.4,10a1.6,1.6 0 1 0 3.2,0a1.6,1.6 0 1 0 -3.2,0Z M12.4,10a1.6,1.6 0 1 0 3.2,0a1.6,1.6 0 1 0 -3.2,0Z M16.4,10a1.6,1.6 0 1 0 3.2,0a1.6,1.6 0 1 0 -3.2,0Z M1,4h18v1h-18Z M1,15h18v1h-18Z", 20, 20);

    /// <summary>围棋棋盘与棋子。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition Go { get; } = new(
        "M2,5h16v1h-16Z M2,10h16v1h-16Z M2,15h16v1h-16Z M5,2h1v16h-1Z M10,2h1v16h-1Z M15,2h1v16h-1Z M3.4,6a2.6,2.6 0 1 0 5.2,0a2.6,2.6 0 1 0 -5.2,0Z M12.4,15a2.6,2.6 0 1 0 5.2,0a2.6,2.6 0 1 0 -5.2,0Z M13.7,15a1.3,1.3 0 1 0 2.6,0a1.3,1.3 0 1 0 -2.6,0Z", 20, 20);

    /// <summary>圆形象棋子与王字。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition Xiangqi { get; } = new(
        "M2,10a8,8 0 1 0 16,0a8,8 0 1 0 -16,0Z M4,10a6,6 0 1 0 12,0a6,6 0 1 0 -12,0Z M6,6h8v1.5h-8Z M6,9.3h8v1.5h-8Z M6,13h8v1.5h-8Z M9.3,6h1.5v8h-1.5Z", 20, 20);

    /// <summary>数字合并方块。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition MergeTiles { get; } = new(
        "M1,3h7v14h-7Z M2.5,4.5h4.0v11.0h-4.0Z M12,3h7v14h-7Z M13.5,4.5h4.0v11.0h-4.0Z M8,8h2V6l3,4l-3,4v-2H8Z", 20, 20);

    /// <summary>九宫格与已填数字格。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition Sudoku { get; } = new(
        "M2,2h16v16h-16Z M4,4h12v12h-12Z M7,3h1v14h-1Z M12,3h1v14h-1Z M3,7h14v1h-14Z M3,12h14v1h-14Z M4,4h2v2h-2Z M9,9h2v2h-2Z M14,14h2v2h-2Z", 20, 20);

    /// <summary>推箱子与移动方向。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition Sokoban { get; } = new(
        "M9,4h10v12h-10Z M10.5,5.5h7.0v9.0h-7.0Z M11,6l6,8l-1,1l-6,-8Z M17,6l1,1l-6,8l-1,-1Z M1,9h4V6l4,4l-4,4v-3H1Z", 20, 20);

    /// <summary>俄罗斯方块 T 形。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition Tetris { get; } = new(
        "M2,6h3v3h-3Z M6,6h3v3h-3Z M10,6h3v3h-3Z M6,10h3v3h-3Z M14,2h3v3h-3Z M14,6h3v3h-3Z M14,10h3v3h-3Z M14,14h3v3h-3Z", 20, 20);

    /// <summary>空当槽位与红心纸牌。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition Freecell { get; } = new(
        "M1,2h7v7h-7Z M2.5,3.5h4.0v4.0h-4.0Z M10,2h9v16h-9Z M11.5,3.5h6.0v13.0h-6.0Z M14.5,8c-4,-4 -6,2 0,6c6,-4 4,-10 0,-6Z", 20, 20);

    /// <summary>三枚相邻宝石。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition MatchThree { get; } = new(
        "M3,6l3,4l-3,4l-3,-4Z M10,6l3,4l-3,4l-3,-4Z M17,6l3,4l-3,4l-3,-4Z", 20, 20);

    /// <summary>六角星跳棋棋盘。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition ChineseCheckers { get; } = new(
        "M10,1l3,5h6l-3,4l3,4h-6l-3,5l-3,-5H1l3,-4l-3,-4h6Z M7,10a3,3 0 1 0 6,0a3,3 0 1 0 -6,0Z", 20, 20);

    /// <summary>三维魔方分面。公共八图标无法准确表达该语义，使用原创 20×20 填充路径。</summary>
    internal static VectorIconDefinition RubiksCube { get; } = new(
        "M10,1l9,5v8l-9,5l-9,-5V6Z M10,3L4,6l6,3l6,-3Z M3,8v5l6,3V11Z M11,11v5l6,-3V8Z M9,4h2v3H9Z M5,10l2,1v3l-2,-1Z M13,11l2,-1v3l-2,1Z", 20, 20);

}
