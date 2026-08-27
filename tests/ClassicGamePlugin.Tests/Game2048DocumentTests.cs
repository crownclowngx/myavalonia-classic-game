using ClassicGamePlugin.Features.Game2048;
using ClassicGamePlugin.Features.Game2048.ViewModels;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Game2048DocumentTests
{
    [Fact]
    public async Task 空白Host标题保留默认标题()
    {
        var document = CreateDocument();

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);

        Assert.Equal("2048", document.Presentation.Title);
    }

    [Fact]
    public async Task 初始化采用Host标题并且只在变化时通知()
    {
        var document = CreateDocument();
        var notificationCount = 0;
        document.PresentationChanged += (_, _) => notificationCount++;

        await document.InitializeAsync(new NewDocumentActivation("我的 2048"), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation("我的 2048"), CancellationToken.None);

        Assert.Equal("我的 2048", document.Presentation.Title);
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public void Document只持有独立ViewModel且不伪造释放接口()
    {
        var first = CreateDocument();
        var second = CreateDocument();

        Assert.IsType<Game2048ViewModel>(first.ViewModel);
        Assert.NotSame(first.ViewModel, second.ViewModel);
        Assert.IsNotAssignableFrom<IPluginDocument>(first.ViewModel);
        Assert.IsNotAssignableFrom<IDisposable>(first);
        Assert.Null(typeof(Game2048Document).GetProperty("BoardCells"));

        first.ViewModel.AnimationsEnabled = false;
        Assert.False(first.ViewModel.AnimationsEnabled);
        Assert.True(second.ViewModel.AnimationsEnabled);
    }

    private static Game2048Document CreateDocument() =>
        new(new FirstEmptyTileSpawnStrategy(2, 4));
}
