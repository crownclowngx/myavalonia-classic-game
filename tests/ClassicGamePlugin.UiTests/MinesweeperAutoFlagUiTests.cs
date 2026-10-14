using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using ClassicGamePlugin.Features.Minesweeper.Domain;
using ClassicGamePlugin.Features.Minesweeper.ViewModels;
using ClassicGamePlugin.Features.Minesweeper.Views;
using Xunit;

namespace ClassicGamePlugin.UiTests;

public sealed class MinesweeperAutoFlagUiTests
{
    [AvaloniaTheory]
    [InlineData(false, 1100)]
    [InlineData(true, 1100)]
    [InlineData(false, 600)]
    [InlineData(true, 600)]
    public void 自动插旗开关在真实消息循环中双向绑定并渲染(bool dark, int width)
    {
        using var viewModel = new MinesweeperViewModel(new BarrierMines(), TimeProvider.System, false);
        var view = new MinesweeperView { DataContext = viewModel };
        var window = new Window
        {
            Width = width, Height = 650, Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var toggle = Assert.IsType<CheckBox>(view.FindControl<CheckBox>("AutoFlagToggle"));
            Assert.False(toggle.IsChecked);
            viewModel.RevealCell(viewModel.BoardCells.Single(cell => cell.Row == 8 && cell.Column == 8));
            toggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.IsAutoFlagEnabled);
            Assert.Equal(9, viewModel.LastAutoFlagCount);
            Assert.Equal(1, viewModel.RemainingMineCount);
            Assert.False(viewModel.BoardCells.Single(cell => cell.Row == 0 && cell.Column == 1).IsRevealed);
            Assert.True(toggle.Bounds.Width > 0);
            var gameStatus = Assert.IsType<TextBlock>(view.FindControl<TextBlock>("GameStatus"));
            Assert.Equal("进行中", gameStatus.Text);
            Assert.True(gameStatus.Bounds.Width > 0);
            Assert.Equal("本次自动插旗 9 格", view.FindControl<TextBlock>("AutoFlagStatus")!.Text);
            var position = toggle.TranslatePoint(default, window);
            Assert.NotNull(position);
            Assert.InRange(position.Value.X, 0, window.Bounds.Width - toggle.Bounds.Width);

            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var directory = Path.Combine(RepositoryRoot(), "artifacts/minesweeper-auto-flag/screenshots");
            Directory.CreateDirectory(directory);
            frame.Save(Path.Combine(directory, $"minesweeper-{(dark ? "dark" : "light")}-{width}.png"), PngBitmapEncoderOptions.Default);

            viewModel.IsAutoFlagEnabled = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(toggle.IsChecked);
            Assert.Equal(1, viewModel.RemainingMineCount);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ClassicGamePlugin.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("找不到经典游戏仓库。");
    }

    private sealed class BarrierMines : IMinePlacementStrategy
    {
        public IReadOnlyCollection<CellCoordinate> CreateMines(int rows, int columns, int mineCount,
            IReadOnlySet<CellCoordinate> excludedCoordinates) => Enumerable.Range(0, 9)
                .Select(column => new CellCoordinate(1, column)).Append(new CellCoordinate(0, 0)).ToArray();
    }
}
