using Avalonia.Controls;

namespace ClassicGamePlugin.Features.RubiksCube.Views;

/// <summary>SDK 边界包装 View，单向传递 Document 拥有的 ViewModel，不另建棋局。</summary>
public partial class RubiksCubeDocumentView : UserControl
{
    public RubiksCubeDocumentView() => InitializeComponent();
    internal object? HostedViewModel => ViewModelHost.Content;
}
