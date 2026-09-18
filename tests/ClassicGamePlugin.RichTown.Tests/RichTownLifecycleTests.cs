using ClassicGamePlugin.Features.RichTown;
using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Rendering;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

/// <summary>对原生边界注入可控故障。验证资源所有权和窗口迁移语义，不启动 GPU、不靠休眠等待。</summary>
public sealed class RichTownLifecycleTests
{
    [Fact]
    public void 隐藏和零尺寸不绘制且停止后可以重建而关闭后不能复活()
    {
        var surfaces = new List<FakeSurface>();
        using var controller = new RichTownSurfaceController(() => { var s = new FakeSurface(); surfaces.Add(s); return s; }, new());
        controller.Start(1);
        controller.Start(1);
        controller.Render(false, 100, 80);
        controller.Render(true, 0, 80);
        controller.Render(true, 100, -1);
        Assert.Equal(0, surfaces[0].Frames);
        controller.Render(true, 300, 200);
        Assert.Equal((300, 200), surfaces[0].Size);
        controller.Rotate(0.4f);
        controller.Rotate(float.NaN);
        Assert.Equal(0.4f, surfaces[0].Rotation);
        controller.Stop();
        controller.Render(true, 300, 200);
        Assert.Equal(1, surfaces[0].Frames);
        controller.Start(2);
        Assert.Equal(2, surfaces.Count);
        Assert.Equal(1, surfaces[0].Disposals);
        controller.Dispose();
        controller.Dispose();
        controller.Start(3);
        controller.Render(true, 300, 200);
        Assert.Equal(2, surfaces.Count);
        Assert.Equal(1, surfaces[1].Disposals);
        Assert.Equal(0, surfaces[1].Frames);
    }

    [Theory]
    [InlineData("factory")]
    [InlineData("start")]
    [InlineData("render")]
    [InlineData("rotate")]
    public void 任一阶段失败均清理已创建对象并归还租约(string failure)
    {
        var lease = new RichTownSurfaceLease();
        var fake = new FakeSurface { Failure = failure };
        using var failed = new RichTownSurfaceController(() => failure == "factory" ? throw new IOException("factory") : fake, lease);
        failed.Start(1);
        failed.Render(true, 100, 100);
        failed.Rotate(1);
        Assert.False(failed.IsRunning);
        Assert.Contains(failure, failed.Error);
        Assert.Equal(failure == "factory" ? 0 : 1, fake.Disposals);
        using var next = new RichTownSurfaceController(() => new FakeSurface(), lease);
        next.Start(2);
        Assert.True(next.IsRunning);
    }

    [Fact]
    public void 清理也失败时保留原始错误且不会永久占用租约()
    {
        var lease = new RichTownSurfaceLease();
        using var controller = new RichTownSurfaceController(() => new FakeSurface { Failure = "start", FailDisposal = true }, lease);
        controller.Start(1);
        Assert.Contains("start", controller.Error);
        Assert.Contains("dispose", controller.Error);
        Assert.True(lease.TryAcquire());
        lease.Release();
    }

    [Fact]
    public void 两个打开请求只启动一个且未取得租约的关闭不能释放别人的租约()
    {
        var lease = new RichTownSurfaceLease();
        using var first = new RichTownSurfaceController(() => new FakeSurface(), lease);
        using var second = new RichTownSurfaceController(() => throw new Exception("不应调用工厂"), lease);
        first.Start(1);
        second.Start(2);
        Assert.True(first.IsRunning);
        Assert.False(second.IsRunning);
        Assert.Contains("已有一个", second.Error);
        second.Dispose();
        Assert.False(lease.TryAcquire());
        first.Dispose();
        Assert.True(lease.TryAcquire());
        lease.Release();
    }

    [Fact]
    public void 租约并发竞争只有一个成功者()
    {
        var lease = new RichTownSurfaceLease();
        var successes = 0;
        Parallel.For(0, 64, _ => { if (lease.TryAcquire()) Interlocked.Increment(ref successes); });
        Assert.Equal(1, successes);
        lease.Release();
        Assert.True(lease.TryAcquire());
        lease.Release();
    }

    [Fact]
    public void 无句柄不分配资源且未启动对象可以安全重复关闭()
    {
        var lease = new RichTownSurfaceLease();
        using var controller = new RichTownSurfaceController(() => throw new Exception("不应调用工厂"), lease);
        controller.Start(0);
        Assert.Contains("有效窗口", controller.Error);
        controller.Dispose();
        controller.Dispose();
        Assert.True(lease.TryAcquire());
        lease.Release();
    }

    [Fact]
    public async Task 文档替换与迟到绑定均遵守独立所有权并且标题通知不重复()
    {
        var first = new FakeSurface();
        var replacement = new FakeSurface();
        var late = new FakeSurface();
        using var document = new RichTownDocument(RichTownSnapshot.Create(19), new());
        var changes = 0;
        document.PresentationChanged += (_, _) => changes++;
        await document.InitializeAsync(new NewDocumentActivation("小镇检查"), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await document.InitializeAsync(new NewDocumentActivation("小镇检查"), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await document.InitializeAsync(new NewDocumentActivation(" "), CancellationToken.None));
        Assert.Equal(1, changes);
        Assert.Equal("小镇检查", document.Presentation.Title);
        document.AttachSurface(first);
        document.AttachSurface(first);
        document.AttachSurface(replacement);
        Assert.Equal(1, first.Disposals);
        Assert.Equal(0, replacement.Disposals);
        document.Dispose();
        document.Dispose();
        document.AttachSurface(late);
        Assert.Equal(1, replacement.Disposals);
        Assert.Equal(1, late.Disposals);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await document.InitializeAsync(new NewDocumentActivation("迟到"), CancellationToken.None));
    }

    [Fact]
    public async Task 取消初始化不改变文档状态()
    {
        using var document = new RichTownDocument();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await document.InitializeAsync(new NewDocumentActivation("不采用"), new CancellationToken(true)));
        Assert.Equal("富翁小镇 3D（开发版）", document.Presentation.Title);
        Assert.True(typeof(IPersistablePluginDocument).IsAssignableFrom(typeof(RichTownDocument)));
    }

    private sealed class FakeSurface : IRichTownSurface
    {
        public string? Failure { get; init; }
        public bool FailDisposal { get; init; }
        public int Frames { get; private set; }
        public int Disposals { get; private set; }
        public (int, int) Size { get; private set; }
        public float Rotation { get; private set; }
        public void Start(nint handle) { if (Failure == "start") throw new IOException("start"); }
        public void Render(int width, int height) { if (Failure == "render") throw new IOException("render"); Frames++; Size = (width, height); }
        public void Rotate(float radians) { if (Failure == "rotate") throw new IOException("rotate"); Rotation += radians; }
        public void Dispose() { Disposals++; if (FailDisposal) throw new IOException("dispose"); }
    }
}
