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
}
