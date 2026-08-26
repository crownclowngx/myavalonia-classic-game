using MyAvaloniaManagement.PluginSdk.UI;
using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.Minesweeper.Views;
using ClassicGamePlugin.Features.SpiderSolitaire;
using ClassicGamePlugin.Features.SpiderSolitaire.Views;
using ClassicGamePlugin.Features.Reversi;
using ClassicGamePlugin.Features.Reversi.Views;
using ClassicGamePlugin.Features.Gomoku;
using ClassicGamePlugin.Features.Gomoku.Views;

namespace ClassicGamePlugin.Plugin;

public sealed class ClassicGamePluginModule : IPluginModule
{
    public void Configure(IPluginRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        registration.Services.AddClassicGamePluginServices();
        registration.AddDocument<MinesweeperDocument, MinesweeperDocumentView>(
            new DocumentDescriptor(
                PluginIds.MinesweeperDocument,
                "扫雷",
                "经典扫雷游戏：翻开安全格、标记地雷并完成整张棋盘",
                "经典游戏"));
        registration.AddDocument<SpiderSolitaireDocument, SpiderSolitaireDocumentView>(
            new DocumentDescriptor(
                PluginIds.SpiderSolitaireDocument,
                "蜘蛛纸牌",
                "经典蜘蛛纸牌：整理同花色连续牌组并完成八组 K 到 A",
                "经典游戏"));
        registration.AddDocument<ReversiDocument, ReversiDocumentView>(
            new DocumentDescriptor(
                PluginIds.ReversiDocument,
                "黑白棋",
                "经典黑白棋：夹住并翻转对方棋子，占据更多棋盘位置",
                "经典游戏"));
        registration.AddDocument<GomokuDocument, GomokuDocumentView>(
            new DocumentDescriptor(
                PluginIds.GomokuDocument,
                "五子棋",
                "经典五子棋：自由或禁手规则下连成五子，支持双人与三级人机",
                "经典游戏"));
    }
}
