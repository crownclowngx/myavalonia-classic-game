using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassicGamePlugin.Features.RichTown.Rendering;

namespace ClassicGamePlugin.Features.RichTown.Views;

/// <summary>
/// G1 共用检查页面：Standalone 和真实 Host 使用完全相同的视口，避免两个入口产生不同适配逻辑。
/// 按钮仅用于输入、重建及原生窗口叠层实验，完成 G1 后再由正式游戏页面替换。
/// </summary>
public sealed class RichTownDocumentView : UserControl
{
    private readonly RichTownPrototypeViewport _viewport = new();

    public RichTownDocumentView()
    {
        var status = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 100, Text = "正在初始化小镇…" };
        _viewport.StatusChanged += message => status.Text = message;
        var overlay = new Border
        {
            IsVisible = false, Width = 360, Height = 140, Background = Brushes.DarkRed,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "叠层检查：红框必须完整盖住房屋", Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center },
        };
        var stage = new Grid();
        stage.Children.Add(_viewport);
        stage.Children.Add(overlay);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var rotate = new Button { Content = "旋转房屋" };
        rotate.Click += (_, _) => _viewport.RotateHouse(0.4f);
        var restart = new Button { Content = "重建视口" };
        restart.Click += (_, _) => _viewport.RestartSurface();
        var layering = new Button { Content = "切换叠层检查" };
        layering.Click += (_, _) =>
        {
            overlay.IsVisible = !overlay.IsVisible;
            layering.Content = overlay.IsVisible ? "关闭叠层检查（已开启）" : "切换叠层检查";
        };
        buttons.Children.Add(rotate);
        buttons.Children.Add(restart);
        buttons.Children.Add(layering);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), RowSpacing = 10, Margin = new Thickness(16) };
        grid.Children.Add(new TextBlock { Text = "富翁小镇 3D · G1 集成原型", FontSize = 24 });
        Grid.SetRow(buttons, 1); grid.Children.Add(buttons);
        Grid.SetRow(stage, 2); grid.Children.Add(stage);
        Grid.SetRow(status, 3); grid.Children.Add(status);
        Content = grid;
        DataContextChanged += (_, _) =>
        {
            if (DataContext is RichTownDocument document) document.AttachSurface(_viewport);
        };
    }
}
