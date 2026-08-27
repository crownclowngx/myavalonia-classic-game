using Avalonia.Controls;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.SpiderSolitaire;
using ClassicGamePlugin.Features.Reversi;
using ClassicGamePlugin.Features.Gomoku;
using ClassicGamePlugin.Features.Xiangqi;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Standalone;

public sealed partial class MainWindow : Window
{
    private readonly MinesweeperDocument _minesweeperDocument;
    private readonly SpiderSolitaireDocument _spiderSolitaireDocument;
    private readonly ReversiDocument _reversiDocument;
    private readonly GomokuDocument _gomokuDocument;
    private readonly XiangqiDocument _xiangqiDocument;

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
    }

    /// <summary>Standalone 明确拥有五个预览 Document，窗口关闭时按 Host 的语义释放局内计时和后台资源。</summary>
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
