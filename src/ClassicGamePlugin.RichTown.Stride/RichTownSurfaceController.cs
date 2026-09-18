namespace ClassicGamePlugin.Features.RichTown.Rendering;

/// <summary>小镇原生表面的最小边界。单测替换此边界，不需要创建显卡或 HWND。</summary>
internal interface IRichTownSurface : IDisposable
{
    void Start(nint handle);
    void Render(int width, int height);
    void Rotate(float radians);
}

/// <summary>G3 独有的呈现/镜头边界；G1 生命周期替身无需实现这些能力，避免把诊断与游戏操作塞入万能接口。</summary>
internal interface IRichTownPlayableSurface
{
    event Action<int>? CellSelected;
    void Present(RichTownSceneFrame frame);
    void SetInputEnabled(bool enabled);
    void ResetCamera();
    void Zoom(float steps);
}

/// <summary>
/// 一个插件加载上下文内的小镇只允许一个原生表面。租约不存放棋盘、玩家或其他游戏状态。
/// 原子获取保证两个同时到达的打开请求不会启动两个 SDL 消息泵；实例化的租约也方便独立测试。
/// </summary>
internal sealed class RichTownSurfaceLease
{
    internal static RichTownSurfaceLease Shared { get; } = new();
    private int _occupied;
    internal bool TryAcquire() => Interlocked.CompareExchange(ref _occupied, 1, 0) == 0;
    internal void Release() => Interlocked.Exchange(ref _occupied, 0);
}

/// <summary>
/// 只负责表面生命周期，不负责游戏规则或 Avalonia 控件。窗口迁移用 Stop/Start，最终关闭用 Dispose。
/// 所有方法由 UI 线程串行调用；渲染、初始化和释放失败都收敛为本页面状态，不抛入宿主事件循环。
/// </summary>
internal sealed class RichTownSurfaceController(Func<IRichTownSurface> factory, RichTownSurfaceLease lease) : IDisposable
{
    private IRichTownSurface? _surface;
    private bool _leased;
    private bool _disposed;
    internal bool IsRunning => _surface is not null;
    internal string? Error { get; private set; }
    internal event Action<int>? CellSelected;

    internal void Start(nint handle)
    {
        if (_disposed || IsRunning) return;
        Error = null;
        if (handle == 0) { Error = "小镇尚未取得有效窗口。"; return; }
        if (!lease.TryAcquire()) { Error = "已有一个小镇视口正在使用，请关闭后重试。"; return; }
        _leased = true;
        try
        {
            // 先保存对象再启动，确保中途失败也能够释放已经分配的设备与缓存。
            _surface = factory();
            if (_surface is IRichTownPlayableSurface playable) playable.CellSelected += ForwardSelection;
            _surface.Start(handle);
        }
        catch (Exception exception) { Fail(exception); }
    }

    internal void Render(bool visible, int width, int height)
    {
        if (_disposed || !visible || width <= 0 || height <= 0) return;
        try { _surface?.Render(width, height); }
        catch (Exception exception) { Fail(exception); }
    }

    internal void Rotate(float radians)
    {
        if (!float.IsFinite(radians) || _disposed) return;
        try { _surface?.Rotate(radians); }
        catch (Exception exception) { Fail(exception); }
    }

    private void ForwardSelection(int index) => CellSelected?.Invoke(index);
    internal void Present(RichTownSceneFrame frame, bool inputEnabled) => UsePlayable(surface => { surface.Present(frame); surface.SetInputEnabled(inputEnabled); });
    internal void ResetCamera() => UsePlayable(surface => surface.ResetCamera());
    internal void Zoom(float steps)
    {
        if (float.IsFinite(steps)) UsePlayable(surface => surface.Zoom(steps));
    }
    private void UsePlayable(Action<IRichTownPlayableSurface> action)
    {
        if (_disposed || _surface is null) return;
        try
        {
            if (_surface is not IRichTownPlayableSurface playable) throw new InvalidOperationException("当前视口不支持可玩场景。");
            action(playable);
        }
        catch (Exception exception) { Fail(exception); }
    }

    private void Fail(Exception exception)
    {
        Error = exception.ToString();
        Stop();
    }

    internal void Stop()
    {
        var surface = _surface;
        _surface = null;
        if (surface is IRichTownPlayableSurface playable) playable.CellSelected -= ForwardSelection;
        try { surface?.Dispose(); }
        catch (Exception exception) { Error = $"{Error}\n小镇释放失败：{exception}".Trim(); }
        finally
        {
            if (_leased) lease.Release();
            _leased = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        CellSelected = null;
    }
}
