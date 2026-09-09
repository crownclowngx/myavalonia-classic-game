using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassicGamePlugin.Features.RubiksCube.ViewModels;

namespace ClassicGamePlugin.Features.RubiksCube.Views;

/// <summary>负责页面布局与视角按钮；宽窄窗口共用同一个 ViewModel 和 3D 控件。</summary>
public partial class RubiksCubeView : UserControl
{
    private bool? _compact;
    public RubiksCubeView() => InitializeComponent();
    internal RubiksCubeViewModel? HostedViewModel => DataContext as RubiksCubeViewModel;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BoundsProperty || LayoutRoot is null) return;
        UpdateLayoutForWidth(Bounds.Width);
    }

    /// <summary>按可用宽度切换真实布局容器；独立入口便于无窗口单测验证布局约束。</summary>
    internal void UpdateLayoutForWidth(double width)
    {
        var compact = width < 850;
        if (_compact == compact) return;
        _compact = compact;
        LayoutRoot.ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "1.2*,*");
        LayoutRoot.RowDefinitions = new RowDefinitions(compact ? "Auto,Auto" : "Auto");
        Grid.SetColumn(TeachingPanel, compact ? 0 : 1);
        Grid.SetRow(TeachingPanel, compact ? 1 : 0);
        CubeViewport.Height = compact ? 380 : 450;
    }

    private void OnResetView(object? sender, RoutedEventArgs args) => CubeViewport.ResetView();
}
