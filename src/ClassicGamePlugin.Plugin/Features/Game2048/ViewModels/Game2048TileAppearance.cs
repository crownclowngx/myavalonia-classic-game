using System.Globalization;
using Avalonia.Media;

namespace ClassicGamePlugin.Features.Game2048.ViewModels;

/// <summary>
/// 统一提供落定方块和动画临时方块的视觉值，避免 View 复制色板和字号分支。
/// 该帮助类型只解释数值的表现形式，不读取或修改游戏状态。
/// </summary>
internal static class Game2048TileAppearance
{
    internal static readonly IBrush EmptyBrush = Brush.Parse("#CDC1B4");
    private static readonly IBrush ExtendedTileBrush = Brush.Parse("#3C3A32");
    private static readonly IBrush DarkTextBrush = Brush.Parse("#776E65");
    private static readonly IBrush LightTextBrush = Brush.Parse("#F9F6F2");
    private static readonly IReadOnlyDictionary<int, IBrush> TileBrushes =
        new Dictionary<int, IBrush>
        {
            [2] = Brush.Parse("#EEE4DA"),
            [4] = Brush.Parse("#EDE0C8"),
            [8] = Brush.Parse("#F2B179"),
            [16] = Brush.Parse("#F59563"),
            [32] = Brush.Parse("#F67C5F"),
            [64] = Brush.Parse("#F65E3B"),
            [128] = Brush.Parse("#EDCF72"),
            [256] = Brush.Parse("#EDCC61"),
            [512] = Brush.Parse("#EDC850"),
            [1024] = Brush.Parse("#EDC53F"),
            [2048] = Brush.Parse("#EDC22E"),
        };

    internal static string GetDisplayText(int value) => value == 0
        ? string.Empty
        : value.ToString(CultureInfo.InvariantCulture);

    internal static IBrush GetBackground(int value) => TileBrushes.TryGetValue(value, out var brush)
        ? brush
        : value == 0 ? EmptyBrush : ExtendedTileBrush;

    internal static IBrush GetForeground(int value) =>
        value is 0 or 2 or 4 ? DarkTextBrush : LightTextBrush;

    internal static double GetFontSize(int value) => GetDisplayText(value).Length switch
    {
        <= 3 => 32,
        4 => 27,
        5 => 22,
        _ => 18,
    };
}
