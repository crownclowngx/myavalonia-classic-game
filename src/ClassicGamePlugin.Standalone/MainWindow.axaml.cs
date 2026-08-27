using Avalonia.Controls;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.SpiderSolitaire;
using ClassicGamePlugin.Features.Reversi;
using ClassicGamePlugin.Features.Gomoku;
using ClassicGamePlugin.Features.Xiangqi;
using ClassicGamePlugin.Features.Game2048;
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
    }

    /// <summary>
    /// Standalone 明确拥有六个预览 Document；窗口关闭时按 Host 的语义释放其中确实拥有计时器、
    /// 动画或后台任务的五个 Document。2048 没有外部资源，因此不增加空洞的释放调用。
    /// </summary>
    protected override void OnClosed(EventArgs eventArgs)
    {
        _minesweeperDocument.Dispose();
        _spiderSolitaireDocument.Dispose();
        _reversiDocument.Dispose();
        _gomokuDocument.Dispose();
        _xiangqiDocument.Dispose();
        base.OnClosed(eventArgs);
    }
}
