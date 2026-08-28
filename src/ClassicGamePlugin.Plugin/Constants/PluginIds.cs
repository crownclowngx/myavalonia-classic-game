using MyAvaloniaManagement.PluginSdk;
using MyAvaloniaManagement.PluginSdk.UI;

namespace ClassicGamePlugin.Constants;

public static class PluginIds
{
    public static readonly PluginId Plugin = new("myavalonia.plugin.classic.game");

    /// <summary>获取“重新开始当前扫雷”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartMinesweeper =
        new("myavalonia.plugin.classic.game.command.minesweeper.restart");

    /// <summary>获取“重新开始当前蜘蛛纸牌牌局”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartSpiderSolitaire =
        new("myavalonia.plugin.classic.game.command.spider-solitaire.restart");

    /// <summary>获取“撤销当前蜘蛛纸牌操作”工作台命令的稳定身份。</summary>
    public static readonly CommandId UndoSpiderSolitaire =
        new("myavalonia.plugin.classic.game.command.spider-solitaire.undo");

    /// <summary>获取“重新开始当前黑白棋”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartReversi =
        new("myavalonia.plugin.classic.game.command.reversi.restart");

    /// <summary>获取“撤销当前黑白棋落子”工作台命令的稳定身份。</summary>
    public static readonly CommandId UndoReversi =
        new("myavalonia.plugin.classic.game.command.reversi.undo");

    /// <summary>获取“重新开始当前五子棋”工作台命令的稳定身份。</summary>
    /// <remarks>
    /// 该值只标识跨菜单和快捷键复用的用户语义，不保存 <see cref="System.Windows.Input.ICommand"/>、
    /// 五子棋 Document 实例或执行回调。Host 会在执行瞬间把身份路由到当前活动的五子棋实例。
    /// </remarks>
    public static readonly CommandId RestartGomoku =
        new("myavalonia.plugin.classic.game.command.gomoku.restart");

    /// <summary>获取“撤销当前五子棋”工作台命令的稳定身份。</summary>
    public static readonly CommandId UndoGomoku =
        new("myavalonia.plugin.classic.game.command.gomoku.undo");

    /// <summary>获取五子棋重新开始命令在 Host Tools 共享菜单中的展示身份。</summary>
    public static readonly CommandPlacementId RestartGomokuMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.gomoku.restart");

    /// <summary>获取五子棋撤销命令在 Host Tools 共享菜单中的展示身份。</summary>
    public static readonly CommandPlacementId UndoGomokuMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.gomoku.undo");

    /// <summary>获取五子棋重新开始命令的快捷键展示身份。</summary>
    public static readonly CommandPlacementId RestartGomokuKeyBinding =
        new("myavalonia.plugin.classic.game.command-placement.keybinding.gomoku.restart");

    /// <summary>获取五子棋撤销命令的快捷键展示身份。</summary>
    public static readonly CommandPlacementId UndoGomokuKeyBinding =
        new("myavalonia.plugin.classic.game.command-placement.keybinding.gomoku.undo");

    /// <summary>获取“重新开始当前围棋”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartGo =
        new("myavalonia.plugin.classic.game.command.go.restart");

    /// <summary>获取“撤销当前围棋落子”工作台命令的稳定身份。</summary>
    public static readonly CommandId UndoGo =
        new("myavalonia.plugin.classic.game.command.go.undo");

    /// <summary>获取“重新开始当前中国象棋”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartXiangqi =
        new("myavalonia.plugin.classic.game.command.xiangqi.restart");

    /// <summary>获取“撤销当前中国象棋着法”工作台命令的稳定身份。</summary>
    public static readonly CommandId UndoXiangqi =
        new("myavalonia.plugin.classic.game.command.xiangqi.undo");

    /// <summary>获取“重新开始当前 2048”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartGame2048 =
        new("myavalonia.plugin.classic.game.command.2048.restart");

    /// <summary>获取“重新开始当前数独题目”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartSudoku =
        new("myavalonia.plugin.classic.game.command.sudoku.restart");

    /// <summary>获取“撤销当前数独输入”工作台命令的稳定身份。</summary>
    public static readonly CommandId UndoSudoku =
        new("myavalonia.plugin.classic.game.command.sudoku.undo");

    /// <summary>获取“重新开始当前推箱子关卡”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartSokoban =
        new("myavalonia.plugin.classic.game.command.sokoban.restart");

    /// <summary>获取“撤销当前推箱子移动”工作台命令的稳定身份。</summary>
    public static readonly CommandId UndoSokoban =
        new("myavalonia.plugin.classic.game.command.sokoban.undo");

    /// <summary>获取“重新开始当前俄罗斯方块”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartTetris =
        new("myavalonia.plugin.classic.game.command.tetris.restart");

    /// <summary>获取“重开当前空当接龙牌局”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartFreeCell =
        new("myavalonia.plugin.classic.game.command.freecell.restart");

    /// <summary>获取“撤销当前空当接龙操作”工作台命令的稳定身份。</summary>
    public static readonly CommandId UndoFreeCell =
        new("myavalonia.plugin.classic.game.command.freecell.undo");

    /// <summary>获取“重新开始当前消消乐”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartMatch3 =
        new("myavalonia.plugin.classic.game.command.match3.restart");

    /// <summary>获取“重新开始当前中国跳棋”工作台命令的稳定身份。</summary>
    public static readonly CommandId RestartChineseCheckers =
        new("myavalonia.plugin.classic.game.command.chinese-checkers.restart");

    /// <summary>获取“撤销当前中国跳棋着法”工作台命令的稳定身份。</summary>
    public static readonly CommandId UndoChineseCheckers =
        new("myavalonia.plugin.classic.game.command.chinese-checkers.undo");

    /// <summary>获取扫雷重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartMinesweeperMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.minesweeper.restart");

    /// <summary>获取蜘蛛纸牌重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartSpiderSolitaireMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.spider-solitaire.restart");

    /// <summary>获取蜘蛛纸牌撤销命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId UndoSpiderSolitaireMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.spider-solitaire.undo");

    /// <summary>获取黑白棋重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartReversiMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.reversi.restart");

    /// <summary>获取黑白棋撤销命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId UndoReversiMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.reversi.undo");

    /// <summary>获取围棋重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartGoMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.go.restart");

    /// <summary>获取围棋撤销命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId UndoGoMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.go.undo");

    /// <summary>获取中国象棋重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartXiangqiMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.xiangqi.restart");

    /// <summary>获取中国象棋撤销命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId UndoXiangqiMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.xiangqi.undo");

    /// <summary>获取 2048 重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartGame2048Menu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.2048.restart");

    /// <summary>获取数独重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartSudokuMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.sudoku.restart");

    /// <summary>获取数独撤销命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId UndoSudokuMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.sudoku.undo");

    /// <summary>获取推箱子重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartSokobanMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.sokoban.restart");

    /// <summary>获取推箱子撤销命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId UndoSokobanMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.sokoban.undo");

    /// <summary>获取俄罗斯方块重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartTetrisMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.tetris.restart");

    /// <summary>获取空当接龙重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartFreeCellMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.freecell.restart");

    /// <summary>获取空当接龙撤销命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId UndoFreeCellMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.freecell.undo");

    /// <summary>获取消消乐重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartMatch3Menu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.match3.restart");

    /// <summary>获取中国跳棋重新开始命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId RestartChineseCheckersMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.chinese-checkers.restart");

    /// <summary>获取中国跳棋撤销命令的 Tools 菜单展示身份。</summary>
    public static readonly CommandPlacementId UndoChineseCheckersMenu =
        new("myavalonia.plugin.classic.game.command-placement.menu.tools.chinese-checkers.undo");

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

    /// <summary>围棋普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId GoDocument =
        new("myavalonia.plugin.classic.game.document.go");

    /// <summary>中国象棋普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId XiangqiDocument =
        new("myavalonia.plugin.classic.game.document.xiangqi");

    /// <summary>2048 普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId Game2048Document =
        new("myavalonia.plugin.classic.game.document.2048");

    /// <summary>数独普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId SudokuDocument =
        new("myavalonia.plugin.classic.game.document.sudoku");

    /// <summary>推箱子普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId SokobanDocument =
        new("myavalonia.plugin.classic.game.document.sokoban");

    /// <summary>俄罗斯方块普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId TetrisDocument =
        new("myavalonia.plugin.classic.game.document.tetris");

    /// <summary>空当接龙普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId FreeCellDocument =
        new("myavalonia.plugin.classic.game.document.freecell");

    /// <summary>消消乐普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId Match3Document =
        new("myavalonia.plugin.classic.game.document.match3");

    /// <summary>中国跳棋普通 Document 的稳定身份。</summary>
    public static readonly DocumentTypeId ChineseCheckersDocument =
        new("myavalonia.plugin.classic.game.document.chinese-checkers");
}
