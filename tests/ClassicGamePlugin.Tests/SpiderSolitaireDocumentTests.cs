using ClassicGamePlugin.Features.SpiderSolitaire;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SpiderSolitaireDocumentTests
{
    [Fact]
    public async Task 空白Host标题保留蜘蛛纸牌默认标题()
    {
        using var document = CreateDocument();

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);

        Assert.Equal("蜘蛛纸牌", document.Presentation.Title);
    }

    [Fact]
    public async Task 初始化采用Host标题并只通知一次变化()
    {
        using var document = CreateDocument();
        var count = 0;
        document.PresentationChanged += (_, _) => count++;

        await document.InitializeAsync(new NewDocumentActivation("我的蜘蛛纸牌"), CancellationToken.None);

        Assert.Equal("我的蜘蛛纸牌", document.Presentation.Title);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Document只拥有独立ViewModel且释放会停止计时()
    {
        var document = CreateDocument();
        var viewModel = document.ViewModel;
        var move = FindFirstLegalMove(viewModel);
        viewModel.Move(move.Source, move.Index, move.Destination);
        Assert.True(viewModel.IsTimerRunning);

        document.Dispose();

        Assert.False(viewModel.IsTimerRunning);
        Assert.Null(typeof(SpiderSolitaireDocument).GetProperty("Columns"));
        Assert.Null(typeof(SpiderSolitaireDocument).GetProperty("UndoCommand"));
    }

    private static SpiderSolitaireDocument CreateDocument() =>
        new(new IdentitySpiderCardShuffler(), new ManualTimeProvider(), false);

    private static (int Source, int Index, int Destination) FindFirstLegalMove(
        ClassicGamePlugin.Features.SpiderSolitaire.ViewModels.SpiderSolitaireViewModel viewModel)
    {
        for (var source = 0; source < 10; source++)
        {
            for (var index = 0; index < viewModel.CurrentSnapshot.Columns[source].Count; index++)
            {
                for (var destination = 0; destination < 10; destination++)
                {
                    if (viewModel.CanMove(source, index, destination))
                    {
                        return (source, index, destination);
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("测试牌序中没有合法移动。");
    }
}
