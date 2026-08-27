using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassicGamePlugin.Features.Game2048.Domain;
using ClassicGamePlugin.Features.Game2048.ViewModels;

namespace ClassicGamePlugin.Features.Game2048.Views;

/// <summary>
/// 2048 的 Avalonia View 只负责布局、局部焦点和输入映射。方向键不会注册为应用级快捷键；
/// 只有焦点位于本 View 或其子控件时，隧道路由处理器才会把八个约定按键转成 ViewModel 命令。
/// </summary>
public partial class Game2048View : UserControl
{
    private DispatcherTimer? _animationTimer;
    private readonly List<AnimationVisual> _animationVisuals = [];
    private Game2048ViewModel? _subscribedViewModel;
    private Game2048AnimationPlan? _animation;
    private long _animationStarted;
    private bool _feedbackVisualsPrepared;
    private bool _isAttachedToVisualTree;

    /// <summary>创建视图并登记局部键盘、ViewModel 订阅和约 60fps 的视觉刷新计时器。</summary>
    public Game2048View()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnViewKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += OnDataContextChanged;
    }

    internal Game2048ViewModel? HostedViewModel => DataContext as Game2048ViewModel;

    /// <summary>进入视觉树时恢复对当前 ViewModel 的动画订阅。</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        _isAttachedToVisualTree = true;
        SubscribeToViewModel(HostedViewModel);
    }

    /// <summary>
    /// 离开视觉树时先解除事件，再把未完成动画直接落定。解除在前可保证缓存方向即使产生新移动，
    /// 也会因为没有动画订阅者而立即刷新，不会留下后台计时器或悬空回放。
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        _isAttachedToVisualTree = false;
        SubscribeToViewModel(null);
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void OnBoardPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        // 棋盘没有可点击格子，但允许玩家通过一次明确点击把后续键盘输入限定到本游戏。
        GameBoard.Focus();
    }

    private void OnViewKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (HandleKey(eventArgs.Key))
        {
            eventArgs.Handled = true;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        // 脱离视觉树的 View 不应持有 ViewModel 事件，也不应因测试或模板预加载而创建计时器。
        // 真正挂载后才订阅；挂载期间若 Host 替换 DataContext，则在这里安全切换订阅对象。
        if (_isAttachedToVisualTree)
        {
            SubscribeToViewModel(HostedViewModel);
        }
    }

    private void SubscribeToViewModel(Game2048ViewModel? viewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, viewModel))
        {
            return;
        }

        var previous = _subscribedViewModel;
        if (previous is not null)
        {
            previous.AnimationRequested -= OnAnimationRequested;
            previous.AnimationCancellationRequested -= OnAnimationCancellationRequested;
        }

        StopVisualAnimation();
        _subscribedViewModel = viewModel;
        if (previous?.IsAnimationRunning == true)
        {
            previous.CompleteAnimation();
        }

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.AnimationRequested += OnAnimationRequested;
            _subscribedViewModel.AnimationCancellationRequested += OnAnimationCancellationRequested;
        }
    }

    private void OnAnimationRequested(object? sender, Game2048AnimationPlan plan)
    {
        StopVisualAnimation();
        _animation = plan;
        _animationStarted = Stopwatch.GetTimestamp();
        _feedbackVisualsPrepared = false;
        SettledTileLayer.IsVisible = false;
        PrepareSlideVisuals(plan);
        RenderAnimationFrame(TimeSpan.Zero);
        EnsureAnimationTimer().Start();
    }

    private void OnAnimationCancellationRequested(object? sender, EventArgs eventArgs) =>
        FinishAnimationImmediately();

    private void OnAnimationTick(object? sender, EventArgs eventArgs)
    {
        if (_animation is null)
        {
            _animationTimer?.Stop();
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(_animationStarted);
        if (_animation.IsComplete(elapsed))
        {
            FinishAnimationImmediately();
            return;
        }

        RenderAnimationFrame(elapsed);
    }

    /// <summary>
    /// 立即清除临时视觉并通知 ViewModel 投影最终棋盘。该入口也供组合测试验证取消行为，
    /// 不依赖等待真实计时器或 Sleep。
    /// </summary>
    internal void FinishAnimationImmediately()
    {
        var viewModel = _subscribedViewModel;
        StopVisualAnimation();
        viewModel?.CompleteAnimation();
    }

    private void RenderAnimationFrame(TimeSpan elapsed)
    {
        if (_animation is null)
        {
            return;
        }

        if (elapsed < Game2048AnimationPlan.SlideDuration)
        {
            var progress = _animation.GetSlideProgress(elapsed);
            foreach (var visual in _animationVisuals)
            {
                SetTileBounds(
                    visual,
                    Interpolate(visual.Source.Column, visual.Target.Column, progress),
                    Interpolate(visual.Source.Row, visual.Target.Row, progress),
                    scale: 1);
            }

            return;
        }

        if (!_feedbackVisualsPrepared)
        {
            PrepareFeedbackVisuals(_animation);
        }

        foreach (var visual in _animationVisuals)
        {
            var scale = visual.IsSpawned
                ? _animation.GetSpawnScale(elapsed)
                : visual.IsMerged ? _animation.GetMergeScale(elapsed) : 1;
            SetTileBounds(visual, visual.Target.Column, visual.Target.Row, scale);
        }
    }

    private void PrepareSlideVisuals(Game2048AnimationPlan plan)
    {
        AnimationLayer.Children.Clear();
        _animationVisuals.Clear();
        foreach (var motion in plan.Transition.Motions)
        {
            AddAnimationVisual(
                motion.Value,
                motion.Source,
                motion.Target,
                isMerged: false,
                isSpawned: false);
        }
    }

    private void PrepareFeedbackVisuals(Game2048AnimationPlan plan)
    {
        AnimationLayer.Children.Clear();
        _animationVisuals.Clear();
        var transition = plan.Transition;
        for (var row = 0; row < Game2048Rules.BoardSize; row++)
        {
            for (var column = 0; column < Game2048Rules.BoardSize; column++)
            {
                var position = new Game2048Position(row, column);
                var value = transition.After.Cells[Game2048Rules.ToIndex(row, column)];
                if (value == 0)
                {
                    continue;
                }

                AddAnimationVisual(
                    value,
                    position,
                    position,
                    transition.MergedPositions.Contains(position),
                    transition.SpawnedTile.Position == position);
            }
        }

        _feedbackVisualsPrepared = true;
    }

    private void AddAnimationVisual(
        int value,
        Game2048Position source,
        Game2048Position target,
        bool isMerged,
        bool isSpawned)
    {
        var scaleTransform = new ScaleTransform(1, 1);
        var tile = new Border
        {
            Background = Game2048TileAppearance.GetBackground(value),
            CornerRadius = new CornerRadius(6),
            IsHitTestVisible = false,
            RenderTransform = scaleTransform,
            RenderTransformOrigin = RelativePoint.Center,
            Child = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontSize = Game2048TileAppearance.GetFontSize(value),
                FontWeight = FontWeight.Bold,
                Foreground = Game2048TileAppearance.GetForeground(value),
                Text = Game2048TileAppearance.GetDisplayText(value),
            },
        };
        var visual = new AnimationVisual(tile, scaleTransform, source, target, isMerged, isSpawned);
        _animationVisuals.Add(visual);
        AnimationLayer.Children.Add(tile);
    }

    private void SetTileBounds(AnimationVisual visual, double column, double row, double scale)
    {
        var cellWidth = AnimationLayer.Bounds.Width / Game2048Rules.BoardSize;
        var cellHeight = AnimationLayer.Bounds.Height / Game2048Rules.BoardSize;
        const double margin = 5;

        Canvas.SetLeft(visual.Tile, (column * cellWidth) + margin);
        Canvas.SetTop(visual.Tile, (row * cellHeight) + margin);
        visual.Tile.Width = Math.Max(0, cellWidth - (margin * 2));
        visual.Tile.Height = Math.Max(0, cellHeight - (margin * 2));
        visual.ScaleTransform.ScaleX = scale;
        visual.ScaleTransform.ScaleY = scale;
    }

    private void StopVisualAnimation()
    {
        _animationTimer?.Stop();
        _animation = null;
        _feedbackVisualsPrepared = false;
        _animationVisuals.Clear();
        AnimationLayer.Children.Clear();
        SettledTileLayer.IsVisible = true;
    }

    private static double Interpolate(double start, double end, double progress) =>
        start + ((end - start) * progress);

    private DispatcherTimer EnsureAnimationTimer()
    {
        if (_animationTimer is not null)
        {
            return _animationTimer;
        }

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _animationTimer.Tick += OnAnimationTick;
        return _animationTimer;
    }

    /// <summary>
    /// 把方向键与 W/A/S/D 映射到同一组命令。返回 false 表示按键不属于游戏，调用方不得吞掉该事件，
    /// 从而保留 Host 的普通文本输入和快捷键行为。
    /// </summary>
    internal bool HandleKey(Key key)
    {
        if (HostedViewModel is not { } viewModel || !TryMapDirection(key, out var direction))
        {
            return false;
        }

        viewModel.Move(direction);
        return true;
    }

    /// <summary>
    /// 纯粹映射本游戏支持的八个按键。拆出该方法后，映射矩阵可以不初始化 Avalonia 视觉树就完成确定性测试；
    /// 返回 false 的按键必须继续交给 Host。
    /// </summary>
    internal static bool TryMapDirection(Key key, out Game2048Direction direction)
    {
        direction = key switch
        {
            Key.Up or Key.W => Game2048Direction.Up,
            Key.Down or Key.S => Game2048Direction.Down,
            Key.Left or Key.A => Game2048Direction.Left,
            Key.Right or Key.D => Game2048Direction.Right,
            _ => default,
        };
        return key is Key.Up or Key.W or Key.Down or Key.S or Key.Left or Key.A or Key.Right or Key.D;
    }

    private sealed record AnimationVisual(
        Border Tile,
        ScaleTransform ScaleTransform,
        Game2048Position Source,
        Game2048Position Target,
        bool IsMerged,
        bool IsSpawned);
}
