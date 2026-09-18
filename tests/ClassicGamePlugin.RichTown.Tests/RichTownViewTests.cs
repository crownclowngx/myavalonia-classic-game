using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ClassicGamePlugin.Features.RichTown;
using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Views;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownViewTests
{
    [Fact]
    public async Task 页面按钮走同一会话并在更换绑定重开关闭后隔离旧状态()
    {
        // 不挂入真实 Window，因此不创建 HWND/GPU。所有 UI 操作在本例同一线程，不排空全局 Dispatcher。
        using var first = new RichTownDocument(RichTownSnapshot.Create(19), new());
        using var next = new RichTownDocument(RichTownSnapshot.Create(0), new());
        await first.InitializeAsync(new MyAvaloniaManagement.PluginSdk.NewDocumentActivation(string.Empty), default);
        await next.InitializeAsync(new MyAvaloniaManagement.PluginSdk.NewDocumentActivation(string.Empty), default);
        var view = new RichTownDocumentView { DataContext = first };
        var buttons = view.GetLogicalDescendants().OfType<Button>().ToDictionary(button => button.Name!);
        void Click(string name) => buttons[name].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(buttons["RollButton"].IsEnabled);
        first.Play.SetActive(true);
        Assert.True(buttons["RollButton"].IsEnabled);
        Click("RollButton");
        Assert.Equal(1, first.Play.Snapshot.Revision);
        Assert.False(buttons["RollButton"].IsEnabled);
        Click("RollButton");
        Assert.Equal(1, first.Play.Snapshot.Revision);
        Click("SkipButton");
        Assert.True(buttons["BuyButton"].IsEnabled);
        Click("BuyButton"); Click("SkipButton");
        Assert.Equal(1400, first.Play.Snapshot.Players[0].Cash);
        Assert.True(buttons["UpgradeButton"].IsEnabled);
        Click("UpgradeButton"); Click("SkipButton");
        Assert.Equal(1, first.Play.Frame.Tiles[1].Level);
        Click("PauseButton");
        Assert.False(buttons["EndTurnButton"].IsEnabled);
        Click("PauseButton");
        Click("EndTurnButton");
        Assert.Equal(1, first.Play.Snapshot.CurrentPlayerId);
        Click("SkipButton");
        Click("NewGameButton");
        Assert.Equal(1, first.Play.Generation);
        Assert.Equal(0, first.Play.Snapshot.Revision);
        view.Measure(new Size(800, 760)); view.Arrange(new Rect(0, 0, 800, 760));
        view.Measure(new Size(1200, 900)); view.Arrange(new Rect(0, 0, 1200, 900));
        view.DataContext = next;
        first.Dispose();
        next.Play.SetActive(true);
        Click("RollButton");
        Assert.Equal(2, next.Play.Snapshot.Players[0].Position);
        Assert.Equal(0, first.Play.Snapshot.Revision);
        next.Dispose();
        Click("NewGameButton"); Click("RollButton");
        Assert.Equal(1, next.Play.Snapshot.Revision);
        view.DataContext = null;
    }
}
