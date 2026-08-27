using Avalonia.Controls;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.SpiderSolitaire;
using ClassicGamePlugin.Features.Reversi;
using ClassicGamePlugin.Features.Gomoku;
using ClassicGamePlugin.Features.Xiangqi;
using ClassicGamePlugin.Features.Game2048;
using ClassicGamePlugin.Features.Sudoku;
using ClassicGamePlugin.Features.Sokoban;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Standalone;

public sealed partial class MainWindow : Window
{
    private readonly MinesweeperDocument _minesweeperDocument;
    private readonly SpiderSolitaireDocument _spiderSolitaireDocument;
    private readonly ReversiDocument _reversiDocument;
    private readonly GomokuDocument _gomokuDocument;
    private readonly XiangqiDocument _xiangqiDocument;
    private readonly Game2048Document _game2048Document;
    private readonly SudokuDocument _sudokuDocument;
    private readonly SokobanDocument _sokobanDocument;

    public MainWindow()
    {
        InitializeComponent();

        _minesweeperDocument = new MinesweeperDocument();
        _minesweeperDocument.InitializeAsync(
            new NewDocumentActivation("扫雷（Standalone）"),
            CancellationToken.None).GetAwaiter().GetResult();
        MinesweeperHost.DataContext = _minesweeperDocument;

        _spiderSolitaireDocument = new SpiderSolitaireDocument();
        _spiderSolitaireDocument.InitializeAsync(
            new NewDocumentActivation("蜘蛛纸牌（Standalone）"),
            CancellationToken.None).GetAwaiter().GetResult();
        SpiderSolitaireHost.DataContext = _spiderSolitaireDocument;

        _reversiDocument = new ReversiDocument();
        _reversiDocument.InitializeAsync(
            new NewDocumentActivation("黑白棋（Standalone）"),
            CancellationToken.None).GetAwaiter().GetResult();
        ReversiHost.DataContext = _reversiDocument;

        _gomokuDocument = new GomokuDocument();
        _gomokuDocument.InitializeAsync(
            new NewDocumentActivation("五子棋（Standalone）"),
            CancellationToken.None).GetAwaiter().GetResult();
        GomokuHost.DataContext = _gomokuDocument;

        _xiangqiDocument = new XiangqiDocument();
        _xiangqiDocument.InitializeAsync(
            new NewDocumentActivation("中国象棋（Standalone）"),
            CancellationToken.None).GetAwaiter().GetResult();
        XiangqiHost.DataContext = _xiangqiDocument;

        _game2048Document = new Game2048Document();
        _game2048Document.InitializeAsync(
            new NewDocumentActivation("2048（Standalone）"),
            CancellationToken.None).GetAwaiter().GetResult();
        Game2048Host.DataContext = _game2048Document;

        _sudokuDocument = new SudokuDocument();
        _sudokuDocument.InitializeAsync(
            new NewDocumentActivation("数独（Standalone）"),
            CancellationToken.None).GetAwaiter().GetResult();
        SudokuHost.DataContext = _sudokuDocument;

        _sokobanDocument = new SokobanDocument();
        _sokobanDocument.InitializeAsync(
            new NewDocumentActivation("推箱子（Standalone）"),
            CancellationToken.None).GetAwaiter().GetResult();
        SokobanHost.DataContext = _sokobanDocument;
    }

    /// <summary>
    /// Standalone 明确拥有八个预览 Document；窗口关闭时按 Host 的语义释放其中确实拥有计时器、
    /// 动画或后台任务的六个 Document。2048 与推箱子的计时器都只属于视觉树，Document 不增加空洞的释放调用。
    /// </summary>
    protected override void OnClosed(EventArgs eventArgs)
    {
        _minesweeperDocument.Dispose();
        _spiderSolitaireDocument.Dispose();
        _reversiDocument.Dispose();
        _gomokuDocument.Dispose();
        _xiangqiDocument.Dispose();
        _sudokuDocument.Dispose();
        base.OnClosed(eventArgs);
    }
}
