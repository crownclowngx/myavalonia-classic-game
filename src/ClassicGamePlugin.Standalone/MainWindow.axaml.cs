using Avalonia.Controls;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.SpiderSolitaire;
using ClassicGamePlugin.Features.Reversi;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Standalone;

public sealed partial class MainWindow : Window
{
    private readonly MinesweeperDocument _minesweeperDocument;
    private readonly SpiderSolitaireDocument _spiderSolitaireDocument;
    private readonly ReversiDocument _reversiDocument;

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
    }

    /// <summary>Standalone 明确拥有两个预览 Document，窗口关闭时按 Host 的语义释放局内计时资源。</summary>
    protected override void OnClosed(EventArgs eventArgs)
    {
        _minesweeperDocument.Dispose();
        _spiderSolitaireDocument.Dispose();
        _reversiDocument.Dispose();
        base.OnClosed(eventArgs);
    }
}
