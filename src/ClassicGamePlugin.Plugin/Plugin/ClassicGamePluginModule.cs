using Avalonia.Input;
using MyAvaloniaManagement.PluginSdk;
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

        // G8 只提升每个游戏已经存在的“重新开始/重开同局”和“撤销”用户意图。没有撤销业务
        // 能力的 2048、扫雷、消消乐和俄罗斯方块只声明 Restart；不会为了表面对称伪造 Undo。
        // 注册表只保存不可变身份、文字和 DocumentTypeId，不捕获 Document、ViewModel 或 ICommand。
        RegisterRestartCommand(registration, "minesweeper", "扫雷",
            PluginIds.MinesweeperDocument, PluginIds.RestartMinesweeper, PluginIds.RestartMinesweeperMenu);
        RegisterRestartCommand(registration, "spider-solitaire", "蜘蛛纸牌",
            PluginIds.SpiderSolitaireDocument, PluginIds.RestartSpiderSolitaire, PluginIds.RestartSpiderSolitaireMenu);
        RegisterUndoCommand(registration, "spider-solitaire", "蜘蛛纸牌",
            PluginIds.SpiderSolitaireDocument, PluginIds.UndoSpiderSolitaire, PluginIds.UndoSpiderSolitaireMenu);
        RegisterRestartCommand(registration, "reversi", "黑白棋",
            PluginIds.ReversiDocument, PluginIds.RestartReversi, PluginIds.RestartReversiMenu);
        RegisterUndoCommand(registration, "reversi", "黑白棋",
            PluginIds.ReversiDocument, PluginIds.UndoReversi, PluginIds.UndoReversiMenu);
        RegisterRestartCommand(registration, "gomoku", "五子棋",
            PluginIds.GomokuDocument, PluginIds.RestartGomoku, PluginIds.RestartGomokuMenu);
        RegisterUndoCommand(registration, "gomoku", "五子棋",
            PluginIds.GomokuDocument, PluginIds.UndoGomoku, PluginIds.UndoGomokuMenu);
        RegisterRestartCommand(registration, "go", "围棋",
            PluginIds.GoDocument, PluginIds.RestartGo, PluginIds.RestartGoMenu);
        RegisterUndoCommand(registration, "go", "围棋",
            PluginIds.GoDocument, PluginIds.UndoGo, PluginIds.UndoGoMenu);
        RegisterRestartCommand(registration, "xiangqi", "中国象棋",
            PluginIds.XiangqiDocument, PluginIds.RestartXiangqi, PluginIds.RestartXiangqiMenu);
        RegisterUndoCommand(registration, "xiangqi", "中国象棋",
            PluginIds.XiangqiDocument, PluginIds.UndoXiangqi, PluginIds.UndoXiangqiMenu);
        RegisterRestartCommand(registration, "2048", "2048",
            PluginIds.Game2048Document, PluginIds.RestartGame2048, PluginIds.RestartGame2048Menu);
        RegisterRestartCommand(registration, "sudoku", "数独",
            PluginIds.SudokuDocument, PluginIds.RestartSudoku, PluginIds.RestartSudokuMenu);
        RegisterUndoCommand(registration, "sudoku", "数独",
            PluginIds.SudokuDocument, PluginIds.UndoSudoku, PluginIds.UndoSudokuMenu);
        RegisterRestartCommand(registration, "sokoban", "推箱子",
            PluginIds.SokobanDocument, PluginIds.RestartSokoban, PluginIds.RestartSokobanMenu);
        RegisterUndoCommand(registration, "sokoban", "推箱子",
            PluginIds.SokobanDocument, PluginIds.UndoSokoban, PluginIds.UndoSokobanMenu);
        RegisterRestartCommand(registration, "tetris", "俄罗斯方块",
            PluginIds.TetrisDocument, PluginIds.RestartTetris, PluginIds.RestartTetrisMenu);
        RegisterRestartCommand(registration, "freecell", "空当接龙",
            PluginIds.FreeCellDocument, PluginIds.RestartFreeCell, PluginIds.RestartFreeCellMenu);
        RegisterUndoCommand(registration, "freecell", "空当接龙",
            PluginIds.FreeCellDocument, PluginIds.UndoFreeCell, PluginIds.UndoFreeCellMenu);
        RegisterRestartCommand(registration, "match3", "消消乐",
            PluginIds.Match3Document, PluginIds.RestartMatch3, PluginIds.RestartMatch3Menu);
        RegisterRestartCommand(registration, "chinese-checkers", "中国跳棋",
            PluginIds.ChineseCheckersDocument, PluginIds.RestartChineseCheckers, PluginIds.RestartChineseCheckersMenu);
        RegisterUndoCommand(registration, "chinese-checkers", "中国跳棋",
            PluginIds.ChineseCheckersDocument, PluginIds.UndoChineseCheckers, PluginIds.UndoChineseCheckersMenu);

        // 快捷键使用 UI SDK 的强类型枚举，不解析字符串 Gesture。Ctrl+Shift+R 避免占用常见刷新键，
        // Ctrl+Z 延续桌面应用的撤销习惯；发生 Host 保留项或跨插件冲突时仍由 Host 统一治理。
        registration.AddKeyBindingContribution(
            new KeyBindingContributionDescriptor(
                PluginIds.RestartGomokuKeyBinding,
                PluginIds.RestartGomoku,
                Key.R,
                KeyModifiers.Control | KeyModifiers.Shift));
        registration.AddKeyBindingContribution(
            new KeyBindingContributionDescriptor(
                PluginIds.UndoGomokuKeyBinding,
                PluginIds.UndoGomoku,
                Key.Z,
                KeyModifiers.Control));
    }

    /// <summary>声明某个游戏已有的重新开始命令及其 Tools 菜单投影。</summary>
    private static void RegisterRestartCommand(
        IPluginRegistration registration,
        string gameKey,
        string gameDisplayName,
        DocumentTypeId targetDocumentTypeId,
        CommandId commandId,
        CommandPlacementId menuPlacementId) =>
        RegisterCommand(
            registration,
            gameKey,
            targetDocumentTypeId,
            commandId,
            menuPlacementId,
            $"重新开始当前{gameDisplayName}",
            $"重新开始当前活动的{gameDisplayName}对局，不影响其他已打开实例。",
            order: 0);

    /// <summary>声明某个游戏确实支持的撤销命令及其 Tools 菜单投影。</summary>
    private static void RegisterUndoCommand(
        IPluginRegistration registration,
        string gameKey,
        string gameDisplayName,
        DocumentTypeId targetDocumentTypeId,
        CommandId commandId,
        CommandPlacementId menuPlacementId) =>
        RegisterCommand(
            registration,
            gameKey,
            targetDocumentTypeId,
            commandId,
            menuPlacementId,
            $"撤销当前{gameDisplayName}",
            $"撤销当前活动{gameDisplayName}实例中的上一项可撤销操作。",
            order: 10);

    /// <summary>
    /// 集中应用所有游戏共享的菜单位置和 fail-closed 展示政策；该方法只创建 Descriptor，
    /// 不执行游戏代码，也不保存运行态，因此 Module 仍保持单一的声明职责。
    /// </summary>
    private static void RegisterCommand(
        IPluginRegistration registration,
        string gameKey,
        DocumentTypeId targetDocumentTypeId,
        CommandId commandId,
        CommandPlacementId menuPlacementId,
        string displayName,
        string description,
        int order)
    {
        registration.AddDocumentCommand(
            new CommandDescriptor(commandId, displayName, description),
            targetDocumentTypeId);
        registration.AddMenuCommandContribution(
            new MenuCommandContributionDescriptor(
                menuPlacementId,
                commandId,
                WorkbenchMenuLocations.ToolsShared,
                group: $"classic-game.{gameKey}",
                order,
                targetUnavailableBehavior: MenuCommandTargetUnavailableBehavior.Hide));
    }
}
