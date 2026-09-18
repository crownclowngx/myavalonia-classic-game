using ClassicGamePlugin.Features.RichTown.Rendering;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownPlayableSurfaceTests
{
    private static RichTownSceneFrame EmptyFrame() => new(0, 0, [], [], 0, 0, 1, 0);

    [Fact]
    public void 显示镜头输入转发与停止后的迟到选择隔离()
    {
        var surfaces = new List<Surface>();
        using var controller = new RichTownSurfaceController(() => { var s = new Surface(); surfaces.Add(s); return s; }, new());
        var selected = -1;
        controller.CellSelected += index => selected = index;
        controller.Present(EmptyFrame(), true);
        controller.Zoom(float.NaN);
        controller.Start(1);
        var frame = EmptyFrame();
        controller.Present(frame, true);
        controller.Zoom(2); controller.ResetCamera();
        surfaces[0].Select(8);
        Assert.Equal(8, selected);
        Assert.Same(frame, surfaces[0].Frame);
        Assert.True(surfaces[0].InputEnabled);
        Assert.Equal(2, surfaces[0].ZoomSteps);
        Assert.Equal(1, surfaces[0].Resets);
        controller.Stop();
        surfaces[0].Select(10);
        Assert.Equal(8, selected);
        controller.Start(2);
        surfaces[1].Select(5);
        Assert.Equal(5, selected);
        controller.Dispose();
        controller.Present(frame, true); controller.Zoom(1); controller.ResetCamera();
        surfaces[1].Select(12);
        Assert.Equal(5, selected);
        Assert.Equal(1, surfaces[0].Disposed);
        Assert.Equal(1, surfaces[1].Disposed);
    }

    [Theory]
    [InlineData("present")]
    [InlineData("input")]
    [InlineData("zoom")]
    [InlineData("reset")]
    public void 画面更新失败局限于视口并归还租约(string failure)
    {
        var surface = new Surface { Failure = failure };
        var lease = new RichTownSurfaceLease();
        using var controller = new RichTownSurfaceController(() => surface, lease);
        controller.Start(1);
        if (failure == "zoom") controller.Zoom(1);
        else if (failure == "reset") controller.ResetCamera();
        else controller.Present(EmptyFrame(), false);
        Assert.False(controller.IsRunning);
        Assert.Contains(failure, controller.Error);
        Assert.Equal(1, surface.Disposed);
        Assert.True(lease.TryAcquire());
        lease.Release();
    }

    [Fact]
    public void 不支持可玩边界的表面明确报错而非默默丢弃帧()
    {
        using var controller = new RichTownSurfaceController(() => new DiagnosticSurface(), new());
        controller.Start(1);
        controller.Present(EmptyFrame(), true);
        Assert.False(controller.IsRunning);
        Assert.Contains("不支持可玩场景", controller.Error);
    }

    private sealed class Surface : IRichTownSurface, IRichTownPlayableSurface
    {
        public string? Failure { get; init; }
        public event Action<int>? CellSelected;
        public RichTownSceneFrame? Frame { get; private set; }
        public bool InputEnabled { get; private set; }
        public float ZoomSteps { get; private set; }
        public int Resets { get; private set; }
        public int Disposed { get; private set; }
        public void Start(nint handle) { }
        public void Render(int width, int height) { }
        public void Rotate(float radians) { }
        public void Present(RichTownSceneFrame frame) { Fail("present"); Frame = frame; }
        public void SetInputEnabled(bool enabled) { Fail("input"); InputEnabled = enabled; }
        public void ResetCamera() { Fail("reset"); Resets++; }
        public void Zoom(float steps) { Fail("zoom"); ZoomSteps += steps; }
        public void Select(int index) => CellSelected?.Invoke(index);
        public void Dispose() => Disposed++;
        private void Fail(string operation) { if (Failure == operation) throw new IOException(operation); }
    }

    private sealed class DiagnosticSurface : IRichTownSurface
    {
        public void Start(nint handle) { }
        public void Render(int width, int height) { }
        public void Rotate(float radians) { }
        public void Dispose() { }
    }
}
