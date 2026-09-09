using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassicGamePlugin.Features.RubiksCube.Domain;
using ClassicGamePlugin.Features.RubiksCube.ViewModels;

namespace ClassicGamePlugin.Features.RubiksCube.Views;

/// <summary>
/// 原生自绘 3D 魔方。控件只负责可见期间的帧驱动、相机与几何绘制，所有转面请求交给 ViewModel。
/// 观察操作只修改 yaw/pitch/zoom，不置换领域面；脱离视觉树时同时退订事件、停止帧源并暂停播放器。
/// </summary>
public sealed class RubiksCubeControl : Control
{
    private RubiksCubeViewModel? _subscribed;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Point? _dragPoint;
    private float _yaw = -0.62f;
    private float _pitch = 0.42f;
    private float _zoom = 1;
    private bool _attached;
    private static readonly Color[] Colors =
        [Color.Parse("#FFD335"), Color.Parse("#FF8B32"), Color.Parse("#28BE88"), Color.Parse("#F2F5FA"), Color.Parse("#ED5364"), Color.Parse("#4594EF")];

    public static readonly StyledProperty<RubiksCubeViewModel?> GameProperty =
        AvaloniaProperty.Register<RubiksCubeControl, RubiksCubeViewModel?>(nameof(Game));
    public RubiksCubeViewModel? Game { get => GetValue(GameProperty); set => SetValue(GameProperty, value); }

    public RubiksCubeControl()
    {
        ClipToBounds = true;
        Focusable = true;
        MinWidth = 280;
        MinHeight = 280;
        _timer.Tick += OnTick;
        AutomationProperties.SetName(this, "三阶魔方 3D 视图，可拖动观察、滚轮缩放；转面请使用下方六面按钮。");
    }

    public void ResetView()
    {
        _yaw = -0.62f;
        _pitch = 0.42f;
        _zoom = 1;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameProperty && _attached) Subscribe(Game);
        if (change.Property == IsVisibleProperty) UpdateFrameSource();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        Subscribe(Game);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false;
        Subscribe(null);
        _dragPoint = null;
        base.OnDetachedFromVisualTree(args);
    }

    private void Subscribe(RubiksCubeViewModel? game)
    {
        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged -= OnGameChanged;
            _subscribed.SuspendView();
        }
        _subscribed = game;
        if (_subscribed is not null) _subscribed.PropertyChanged += OnGameChanged;
        UpdateFrameSource();
        InvalidateVisual();
    }

    private void OnGameChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is "State" or nameof(RubiksCubeViewModel.CurrentStep) or nameof(RubiksCubeViewModel.PlaybackText))
        {
            UpdateFrameSource();
            InvalidateVisual();
        }
    }

    private void UpdateFrameSource()
    {
        if (!_attached || !IsEffectivelyVisible)
        {
            _timer.Stop();
            _subscribed?.SuspendView();
        }
        else if (_subscribed?.Player.IsRunning == true) _timer.Start();
        else _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs args)
    {
        // 父级标签页隐藏不一定改变本控件的 IsVisible；每帧提交前检查有效可见性。
        if (!_attached || !IsEffectivelyVisible) { UpdateFrameSource(); return; }
        _subscribed?.Tick();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#111C30")), null, new Rect(Bounds.Size), 16, 16);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#66040812")), null,
            new Point(Bounds.Width / 2, Bounds.Height * 0.83), Bounds.Width * 0.27, Bounds.Height * 0.055);
        var state = Game?.State ?? CubeRules.Solved();
        var player = Game?.Player;
        var polygons = CubeGeometry.CreateFrame(state, player?.CurrentMove, player?.Progress ?? 0, _yaw, _pitch, Game?.Target);
        foreach (var polygon in polygons)
        {
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(Project(polygon.Vertices[0]), true);
                foreach (var vertex in polygon.Vertices.Skip(1)) path.LineTo(Project(vertex));
                path.EndFigure(true);
            }
            var baseColor = polygon.Color is { } face ? Colors[(int)face] : Color.Parse("#1C2534");
            var light = Math.Clamp(0.69 + 0.31 * Vector3.Dot(polygon.Normal, Vector3.Normalize(new Vector3(-0.3f, 0.7f, 1))), 0.5, 1);
            var fill = new SolidColorBrush(Color.FromRgb((byte)(baseColor.R * light), (byte)(baseColor.G * light), (byte)(baseColor.B * light)));
            var pen = polygon.Highlight ? new Pen(Brushes.Gold, 2) : polygon.Color is null ? new Pen(new SolidColorBrush(Color.Parse("#070E19")), 0.7) : null;
            context.DrawGeometry(fill, pen, geometry);
        }
        DrawFaceLabels(context);
        if (player?.CurrentMove is { } move) DrawTurnArrow(context, move);
    }

    private Point Project(Vector3 point)
    {
        var projected = CubeGeometry.Project(point, (float)Bounds.Width, (float)Bounds.Height, _zoom);
        return new Point(projected.X, projected.Y);
    }

    private void DrawFaceLabels(DrawingContext context)
    {
        var view = CubeGeometry.ViewMatrix(_yaw, _pitch);
        foreach (var face in Enum.GetValues<CubeFace>())
        {
            var normal = Vector3.TransformNormal(CubeGeometry.Vector(CubeRules.Normals[(int)face]), view);
            var center = normal * 1.56f;
            if (Vector3.Dot(normal, CubeGeometry.Camera - center) <= 0) continue;
            var point = Project(center);
            var text = new FormattedText(face.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 15, Brushes.Black);
            context.DrawText(text, new Point(point.X - text.Width / 2, point.Y - text.Height / 2));
        }
    }

    private void DrawTurnArrow(DrawingContext context, CubeMove move)
    {
        var normal = CubeGeometry.Vector(CubeRules.Normals[(int)move.Face]);
        var view = CubeGeometry.ViewMatrix(_yaw, _pitch);
        var transformed = Vector3.TransformNormal(normal, view);
        if (Vector3.Dot(transformed, CubeGeometry.Camera - transformed * 1.7f) <= 0) return;
        var tangent = Math.Abs(normal.Y) > 0.5 ? Vector3.UnitX : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, normal));
        var bitangent = Vector3.Cross(normal, tangent);
        Point? previous = null;
        Point? beforeLast = null;
        for (var index = 0; index <= 18; index++)
        {
            var angle = -move.Turns * (0.4 + index * 0.1);
            var world = normal * 1.59f + (tangent * (float)Math.Cos(angle) + bitangent * (float)Math.Sin(angle)) * 0.63f;
            var current = Project(Vector3.Transform(world, view));
            if (previous is { } from) context.DrawLine(new Pen(Brushes.Gold, 3), from, current);
            beforeLast = previous;
            previous = current;
        }
        if (previous is { } end && beforeLast is { } start)
        {
            var direction = end - start;
            var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            if (length < 0.001) return;
            direction /= length;
            var perpendicular = new Avalonia.Vector(-direction.Y, direction.X);
            context.DrawLine(new Pen(Brushes.Gold, 3), end, end - direction * 10 + perpendicular * 5);
            context.DrawLine(new Pen(Brushes.Gold, 3), end, end - direction * 10 - perpendicular * 5);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs args)
    {
        base.OnPointerPressed(args);
        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragPoint = args.GetPosition(this);
        args.Pointer.Capture(this);
        Focus(NavigationMethod.Pointer);
        args.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs args)
    {
        base.OnPointerMoved(args);
        if (_dragPoint is not { } previous) return;
        var current = args.GetPosition(this);
        _yaw += (float)(current.X - previous.X) * 0.009f;
        _pitch = Math.Clamp(_pitch + (float)(current.Y - previous.Y) * 0.009f, -1.5f, 1.5f);
        _dragPoint = current;
        InvalidateVisual();
        args.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs args)
    {
        base.OnPointerReleased(args);
        _dragPoint = null;
        args.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs args)
    {
        base.OnPointerCaptureLost(args);
        _dragPoint = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs args)
    {
        base.OnPointerWheelChanged(args);
        _zoom = Math.Clamp(_zoom + (float)args.Delta.Y * 0.08f, 0.65f, 1.35f);
        InvalidateVisual();
        args.Handled = true;
    }
}
