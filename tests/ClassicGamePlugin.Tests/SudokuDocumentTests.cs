using ClassicGamePlugin.Features.Sudoku;
using ClassicGamePlugin.Features.Sudoku.ViewModels;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SudokuDocumentTests
{
    [Fact]
    public async Task 空白Host标题保留默认数独标题()
    {
        using var document = CreateDocument();

        await document.InitializeAsync(new NewDocumentActivation(string.Empty), CancellationToken.None);

        Assert.Equal("数独", document.Presentation.Title);
    }

    [Fact]
    public async Task 初始化采用Host标题并且只在变化时通知()
    {
        using var document = CreateDocument();
        var notifications = 0;
        document.PresentationChanged += (_, _) => notifications++;

        await document.InitializeAsync(new NewDocumentActivation("我的数独"), CancellationToken.None);
        await document.InitializeAsync(new NewDocumentActivation("我的数独"), CancellationToken.None);

        Assert.Equal("我的数独", document.Presentation.Title);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void 多个Document拥有独立ViewModel并明确实现释放()
    {
        using var first = CreateDocument();
        using var second = CreateDocument();

        Assert.IsType<SudokuViewModel>(first.ViewModel);
        Assert.NotSame(first.ViewModel, second.ViewModel);
        Assert.IsAssignableFrom<IDisposable>(first);
        first.ViewModel.AnimationsEnabled = false;
        Assert.True(second.ViewModel.AnimationsEnabled);
    }

    private static SudokuDocument CreateDocument() =>
        new(new StubSudokuPuzzleProvider(), new ManualTimeProvider(), enableDisplayRefreshTimer: false);
}
