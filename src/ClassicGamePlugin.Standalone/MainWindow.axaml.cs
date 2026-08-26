using Avalonia.Controls;
using ClassicGamePlugin.Features.Minesweeper;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Standalone;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var document = new MinesweeperDocument();
        document.InitializeAsync(
            new NewDocumentActivation("扫雷（Standalone）"),
            CancellationToken.None).GetAwaiter().GetResult();
        DataContext = document;
    }
}
