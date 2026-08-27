using ClassicGamePlugin.Features.FreeCell;
using ClassicGamePlugin.Features.FreeCell.Domain;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class FreeCellDocumentTests
{
    [Fact]
    public async Task 空白Host标题保留默认标题且初始化可解编号牌局()
    {
        using var document = CreateDocument();

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);

        Assert.Equal("空当接龙", document.Presentation.Title);
        Assert.Equal(1, document.ViewModel.DealNumber);
    }

    [Fact]
    public async Task Host标题只通知一次且重复初始化不重复生成()
    {
        var provider = new FixedFreeCellDealProvider(FreeCellTestData.Deal());
        using var document = CreateDocument(provider);
        var notifications = 0;
        document.PresentationChanged += (_, _) => notifications++;

        await document.InitializeAsync(new NewDocumentActivation("我的空当接龙"), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation("我的空当接龙"), CancellationToken.None);

        Assert.Equal("我的空当接龙", document.Presentation.Title);
        Assert.Equal(1, notifications);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task Document只拥有页面ViewModel且释放后拒绝再次初始化()
    {
        var document = CreateDocument();
        await document.InitializeAsync(new NewDocumentActivation("空当接龙"), CancellationToken.None);

        document.Dispose();

        Assert.False(document.ViewModel.CanInteract);
        Assert.Null(typeof(FreeCellDocument).GetProperty("Tableaus"));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await document.InitializeAsync(new NewDocumentActivation("再次打开"), CancellationToken.None));
    }

    private static FreeCellDocument CreateDocument(FixedFreeCellDealProvider? provider = null) =>
        new(
            provider ?? new FixedFreeCellDealProvider(FreeCellTestData.Deal()),
            new ScriptedFreeCellSolver(FreeCellSolveStatus.Solved),
            new ManualTimeProvider(),
            false);
}
