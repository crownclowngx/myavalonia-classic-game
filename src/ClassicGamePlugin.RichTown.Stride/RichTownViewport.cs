using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Diagnostics;

namespace ClassicGamePlugin.Features.RichTown.Rendering;

/// <summary>
/// 小镇的 Avalonia 原生窗口桥。计时器仅驱动可见且非零尺寸的视口；窗口解绑立即停止，重新绑定重建。
/// 不持有任何其他游戏对象。显卡异常由小镇控制器收敛，最终关闭后不再允许迟到回调启动引擎。
/// </summary>
public class RichTownViewport : NativeControlHost, IDisposable
{
    private readonly DispatcherTimer _frames;
    private readonly RichTownSurfaceController _controller;
    private readonly bool _diagnostic;
    private long _lastTick;
    private RichTownSceneFrame? _sceneFrame;
    private nint _handle;
    private bool _disposed;
    private string? _lastStatus;

    public RichTownViewport() : this(false) { }

    protected RichTownViewport(bool diagnostic)
    {
        _diagnostic = diagnostic;
        _controller = new(() => new RichTownStrideSurface(!diagnostic), RichTownSurfaceLease.Shared);
        _controller.CellSelected += index => CellSelected?.Invoke(index);
        _frames = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, OnFrame);
        _frames.Stop();
    }

    public event Action<string>? StatusChanged;
    public event Action<TimeSpan, bool>? FrameElapsed;
    public event Action<int>? CellSelected;
    public bool IsRunning => _controller.IsRunning;
    public string? Error => _controller.Error;
    /// <summary>有效可见尺寸下提交绘制的次数，仅用于本机资源检查；不是 GPU 帧率或已呈现帧数。</summary>
    public long RenderSubmissions { get; private set; }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        _handle = handle.Handle;
        if (!_disposed)
        {
            _controller.Start(_handle);
            _lastTick = Stopwatch.GetTimestamp();
            _frames.Start();
            ReportStatus();
        }
        return handle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _frames.Stop();
        _controller.Stop();
        FrameElapsed?.Invoke(TimeSpan.Zero, false);
        _handle = 0;
        ReportStatus();
        base.DestroyNativeControlCore(control);
    }

    private void OnFrame(object? sender, EventArgs args)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastTick, now);
        _lastTick = now;
        var top = TopLevel.GetTopLevel(this);
        var scaling = top?.RenderScaling ?? 1;
        var visible = IsEffectivelyVisible && Bounds.Width > 0 && Bounds.Height > 0 && (top is not Window window || window.WindowState != WindowState.Minimized);
        FrameElapsed?.Invoke(elapsed, visible && _controller.IsRunning);
        if (!_diagnostic && _sceneFrame is { } frame) _controller.Present(frame, visible && (top is not Window active || active.IsActive));
        _controller.Render(visible, (int)Math.Ceiling(Bounds.Width * scaling), (int)Math.Ceiling(Bounds.Height * scaling));
        if (visible && _controller.IsRunning) RenderSubmissions++;
        ReportStatus();
    }

    public void Present(RichTownSceneFrame frame)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!_disposed) _sceneFrame = frame;
    }
    public void ResetCamera() { Dispatcher.UIThread.VerifyAccess(); _controller.ResetCamera(); }
    public void Zoom(float steps) { Dispatcher.UIThread.VerifyAccess(); _controller.Zoom(steps); }

    /// <summary>仅用于 G1 故障恢复检查，不是游戏重新开始命令。</summary>
    public void RestartSurface()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed) return;
        _controller.Stop();
        _controller.Start(_handle);
        ReportStatus();
    }

    public void RotateHouse(float radians)
    {
        Dispatcher.UIThread.VerifyAccess();
        _controller.Rotate(radians);
        ReportStatus();
    }

    private void ReportStatus()
    {
        var status = _controller.Error ?? (_controller.IsRunning ? (_diagnostic ? "Stride 房屋诊断场景已启动。" : "棋盘已就绪") : "视口已停止。");
        if (status == _lastStatus) return;
        _lastStatus = status;
        StatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        _frames.Stop();
        _controller.Dispose();
        FrameElapsed?.Invoke(TimeSpan.Zero, false);
        _frames.Tick -= OnFrame;
        ReportStatus();
        StatusChanged = null;
        FrameElapsed = null;
        CellSelected = null;
        _sceneFrame = null;
    }
}

/// <summary>保留 G1 诊断入口，同一原生窗口生命周期实现，不承载正式对局。</summary>
public sealed class RichTownPrototypeViewport : RichTownViewport
{
    public RichTownPrototypeViewport() : base(true) { }
}
