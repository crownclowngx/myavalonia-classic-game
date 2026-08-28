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
using ClassicGamePlugin.Features.Go;
using ClassicGamePlugin.Features.Go.Views;
using ClassicGamePlugin.Features.Xiangqi;
using ClassicGamePlugin.Features.Xiangqi.Views;
using ClassicGamePlugin.Features.Game2048;
using ClassicGamePlugin.Features.Game2048.Views;
using ClassicGamePlugin.Features.Sudoku;
using ClassicGamePlugin.Features.Sudoku.Views;
using ClassicGamePlugin.Features.Sokoban;
using ClassicGamePlugin.Features.Sokoban.Views;
using ClassicGamePlugin.Features.Tetris;
using ClassicGamePlugin.Features.Tetris.Views;
using ClassicGamePlugin.Features.FreeCell;
using ClassicGamePlugin.Features.FreeCell.Views;
using ClassicGamePlugin.Features.Match3;
using ClassicGamePlugin.Features.Match3.Views;
using ClassicGamePlugin.Features.ChineseCheckers;
using ClassicGamePlugin.Features.ChineseCheckers.Views;

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
        registration.AddDocument<GoDocument, GoDocumentView>(
            new DocumentDescriptor(
                PluginIds.GoDocument,
                "围棋",
                "标准 19 路围棋：本地双人、提子、全局同形禁着与中国数子",
                "经典游戏"));
        registration.AddDocument<XiangqiDocument, XiangqiDocumentView>(
            new DocumentDescriptor(
                PluginIds.XiangqiDocument,
                "中国象棋",
                "经典中国象棋：标准休闲规则、中文棋谱、决策点撤销与三级人机",
                "经典游戏"));
        registration.AddDocument<Game2048Document, Game2048DocumentView>(
            new DocumentDescriptor(
                PluginIds.Game2048Document,
                "2048",
                "经典数字合并游戏：移动方块、合并同值数字并挑战 2048",
                "经典游戏"));
        registration.AddDocument<SudokuDocument, SudokuDocumentView>(
            new DocumentDescriptor(
                PluginIds.SudokuDocument,
                "数独",
                "经典 9×9 数独：三级难度、候选笔记、提示与唯一解题目生成",
                "经典游戏"));
        registration.AddDocument<SokobanDocument, SokobanDocumentView>(
            new DocumentDescriptor(
                PluginIds.SokobanDocument,
                "推箱子",
                "经典推箱子：递进地图、键盘移动、不限次数撤销与轻量动画",
                "经典游戏"));
        registration.AddDocument<TetrisDocument, TetrisDocumentView>(
            new DocumentDescriptor(
                PluginIds.TetrisDocument,
                "俄罗斯方块",
                "现代俄罗斯方块：SRS 旋转、暂存、幽灵块、完整计分与逐级加速",
                "经典游戏"));
        registration.AddDocument<FreeCellDocument, FreeCellDocumentView>(
            new DocumentDescriptor(
                PluginIds.FreeCellDocument,
                "空当接龙",
                "经典空当接龙：可解编号牌局、拖放纸牌、求解提示与安全自动收牌",
                "经典游戏"));
        registration.AddDocument<Match3Document, Match3DocumentView>(
            new DocumentDescriptor(
                PluginIds.Match3Document,
                "消消乐",
                "经典消消乐：完整特殊组合、连锁消除、提示与轻量动画",
                "经典游戏"));
        registration.AddDocument<ChineseCheckersDocument, ChineseCheckersDocumentView>(
            new DocumentDescriptor(
                PluginIds.ChineseCheckersDocument,
                "中国跳棋",
                "六角星中国跳棋：稳定连续跳、本地双人、三级人机与轻量路径动画",
                "经典游戏"));
    }
}
