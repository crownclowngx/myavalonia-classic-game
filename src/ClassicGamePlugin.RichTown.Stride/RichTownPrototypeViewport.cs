using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace ClassicGamePlugin.Features.RichTown.Rendering;

/// <summary>
/// 小镇的 Avalonia 原生窗口桥。计时器仅驱动可见且非零尺寸的视口；窗口解绑立即停止，重新绑定重建。
/// 不持有任何其他游戏对象。显卡异常由小镇控制器收敛，最终关闭后不再允许迟到回调启动引擎。
/// </summary>
public sealed class RichTownPrototypeViewport : NativeControlHost, IDisposable
{
    private readonly DispatcherTimer _frames;
    private readonly RichTownSurfaceController _controller = new(() => new RichTownStrideSurface(), RichTownSurfaceLease.Shared);
    private nint _handle;
    private bool _disposed;
    private string? _lastStatus;

    public RichTownPrototypeViewport()
    {
        _frames = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, OnFrame);
        _frames.Stop();
    }

    public event Action<string>? StatusChanged;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        _handle = handle.Handle;
        if (!_disposed)
        {
            _controller.Start(_handle);
            _frames.Start();
            ReportStatus();
        }
        return handle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _frames.Stop();
        _controller.Stop();
        _handle = 0;
        ReportStatus();
        base.DestroyNativeControlCore(control);
    }

    private void OnFrame(object? sender, EventArgs args)
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        _controller.Render(IsEffectivelyVisible, (int)Math.Ceiling(Bounds.Width * scaling), (int)Math.Ceiling(Bounds.Height * scaling));
        ReportStatus();
    }

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
        var status = _controller.Error ?? (_controller.IsRunning ? "Stride 房屋场景已启动；当前为 G1 集成原型，尚无对局规则。" : "视口已停止。");
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
        _frames.Tick -= OnFrame;
        ReportStatus();
        StatusChanged = null;
    }
}
