using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Constants;

public static class PluginIds
{
    public static readonly PluginId Plugin = new("myavalonia.plugin.classic.game");

    /// <summary>扫雷普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId MinesweeperDocument =
        new("myavalonia.plugin.classic.game.document.minesweeper");

    /// <summary>蜘蛛纸牌普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId SpiderSolitaireDocument =
        new("myavalonia.plugin.classic.game.document.spider-solitaire");

    /// <summary>黑白棋普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId ReversiDocument =
        new("myavalonia.plugin.classic.game.document.reversi");

    /// <summary>五子棋普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId GomokuDocument =
        new("myavalonia.plugin.classic.game.document.gomoku");

    /// <summary>中国象棋普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId XiangqiDocument =
        new("myavalonia.plugin.classic.game.document.xiangqi");

    /// <summary>2048 普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId Game2048Document =
        new("myavalonia.plugin.classic.game.document.2048");
}
