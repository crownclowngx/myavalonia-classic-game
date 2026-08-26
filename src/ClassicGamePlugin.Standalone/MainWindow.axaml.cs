using Avalonia.Controls;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.SpiderSolitaire;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Standalone;

public sealed partial class MainWindow : Window
{
    private readonly MinesweeperDocument _minesweeperDocument;
    private readonly SpiderSolitaireDocument _spiderSolitaireDocument;

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
    }

    /// <summary>Standalone 明确拥有两个预览 Document，窗口关闭时按 Host 的语义释放局内计时资源。</summary>
    protected override void OnClosed(EventArgs eventArgs)
    {
        _minesweeperDocument.Dispose();
        _spiderSolitaireDocument.Dispose();
        base.OnClosed(eventArgs);
    }
}
