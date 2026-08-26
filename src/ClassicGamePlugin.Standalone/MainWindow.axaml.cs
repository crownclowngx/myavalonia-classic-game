using Avalonia.Controls;
using ClassicGamePlugin.Features.Main;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Standalone;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var document = new MainDocument();
        document.InitializeAsync(
            new NewDocumentActivation("ClassicGamePlugin Standalone"),
            CancellationToken.None).GetAwaiter().GetResult();
        DataContext = document;
    }
}
