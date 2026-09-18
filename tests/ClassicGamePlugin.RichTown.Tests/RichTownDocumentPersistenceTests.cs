using ClassicGamePlugin.Features.RichTown;
using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MyAvaloniaManagement.PluginSdk;
using Xunit;
using static ClassicGamePlugin.RichTown.Tests.RichTownScenarios;

namespace ClassicGamePlugin.RichTown.Tests;

/// <summary>直接调用锁定 SDK 的真实接口，不用同名自建协议替代保存握手、激活或工作台命令。</summary>
public sealed class RichTownDocumentPersistenceTests
{
    [Fact]
    public async Task 同一稳定修订保存且动画选择暂停不制造脏内容()
    {
        using var document = Create();
        IPersistablePluginDocument sdk = document;
        var dirtyEvents = 0;
        sdk.IsDirtyChanged += (sender, _) => { Assert.Same(document, sender); dirtyEvents++; };
        var clean = await sdk.CaptureSaveSnapshotAsync(default);
        Assert.Equal(0, clean.Revision.Value);
        Assert.False(sdk.IsDirty);
        Roll(document);
        Assert.True(document.Play.IsAnimating);
        var rolled = await sdk.CaptureSaveSnapshotAsync(default);
        Assert.Equal(1, rolled.Revision.Value);
        Assert.Equal(1, RichTownContentCodec.Decode(rolled.Content).Snapshot.Players[0].Position);
        document.Play.SelectCell(9);
        document.Play.TogglePause(); document.Play.TogglePause();
        document.Play.SkipAnimation(document.Play.Stamp);
        var afterAnimation = await sdk.CaptureSaveSnapshotAsync(default);
        Assert.Equal(rolled.Revision, afterAnimation.Revision);
        Assert.Equal(rolled.Content.Payload.GetRawText(), afterAnimation.Content.Payload.GetRawText());
        Assert.Equal(1, dirtyEvents);
        sdk.AcceptChanges(new(99));
        Assert.True(sdk.IsDirty);
        sdk.AcceptChanges(rolled.Revision); sdk.AcceptChanges(rolled.Revision);
        Assert.False(sdk.IsDirty);
        Assert.Equal(2, dirtyEvents);
    }

    [Fact]
    public async Task 捕获后继续操作及重开时旧确认不得清除新修改()
    {
        using var document = Create();
        Roll(document);
        var old = await document.CaptureSaveSnapshotAsync(default);
        document.Play.SkipAnimation(document.Play.Stamp);
        Assert.True(document.Play.SubmitHuman(document.Play.Stamp, new(RichTownCommandKind.Buy)));
        document.AcceptChanges(old.Revision);
        Assert.True(document.IsDirty);
        var bought = await document.CaptureSaveSnapshotAsync(default);
        var staleStamp = document.Play.Stamp;
        await document.ExecuteAsync(RichTownDocument.RestartCommandId, default);
        Assert.Equal(0, document.Play.Snapshot.Revision);
        Assert.False(document.Play.IsAnimating);
        Assert.False(document.Play.SubmitHuman(staleStamp, new(RichTownCommandKind.Roll)));
        document.AcceptChanges(bought.Revision);
        Assert.True(document.IsDirty);
        var restarted = await document.CaptureSaveSnapshotAsync(default);
        Assert.True(restarted.Revision.Value > bought.Revision.Value);
        document.AcceptChanges(restarted.Revision);
        Assert.False(document.IsDirty);
        Assert.Equal(1400, RichTownContentCodec.Decode(bought.Content).Snapshot.Players[0].Cash);
    }

    [Fact]
    public async Task 保存中的动画恢复为暂停的已提交结果且随机数继续一致()
    {
        using var source = Create();
        Roll(source);
        var snapshot = await source.CaptureSaveSnapshotAsync(default);
        using var target = new RichTownDocument(At(seed: 999), new());
        await target.InitializeAsync(new RestoreDocumentActivation("我的小镇", snapshot.Content), default);
        Assert.Equal("我的小镇", target.Presentation.Title);
        Assert.False(target.IsDirty);
        Assert.False(target.Play.IsAnimating);
        Assert.True(target.Play.IsPaused);
        Assert.Equal(1, target.Play.Frame.DieValue);
        Assert.True(target.Play.HasRolled);
        target.Play.SetActive(true);
        target.Play.Tick(TimeSpan.FromHours(3));
        Assert.Equal(StateText(source.Play.Snapshot), StateText(target.Play.Snapshot));
        target.Play.TogglePause();
        source.Play.SkipAnimation(source.Play.Stamp);
        Assert.True(source.Play.SubmitHuman(source.Play.Stamp, new(RichTownCommandKind.Decline)));
        Assert.True(target.Play.SubmitHuman(target.Play.Stamp, new(RichTownCommandKind.Decline)));
        Assert.Equal(StateText(source.Play.Snapshot), StateText(target.Play.Snapshot));
        Assert.Equal(1, (await target.CaptureSaveSnapshotAsync(default)).Revision.Value);
    }

    [Fact]
    public async Task 失败和取消初始化保持原状态且不占用租约()
    {
        var lease = new RichTownSessionLease();
        using var document = new RichTownDocument(At(), lease);
        var original = document.Play.Snapshot;
        await Assert.ThrowsAsync<InvalidDataException>(async () => await document.InitializeAsync(new RestoreDocumentActivation("坏存档", RichTownPersistenceTests.Content("{}")), default));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await document.InitializeAsync(new NewDocumentActivation("取消"), new(true)));
        await Assert.ThrowsAsync<ArgumentException>(async () => await document.InitializeAsync(new NewDocumentActivation("未声明入口", new CreationIntentId("myavalonia.plugin.classic.game.creation.rich-town.other")), default));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await document.InitializeAsync(null!, default));
        Assert.False(document.IsInitialized);
        Assert.Same(original, document.Play.Snapshot);
        Assert.Equal("富翁小镇 3D（开发版）", document.Presentation.Title);
        using (lease.Acquire()) { }
        await document.InitializeAsync(new NewDocumentActivation(" "), default);
        Assert.False(document.IsDirty);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await document.InitializeAsync(new RestoreDocumentActivation("覆盖", RichTownContentCodec.Encode(new(At(seed: 2)))), default));
        Assert.Same(original, document.Play.Snapshot);
    }

    [Fact]
    public void 并发激活只能一个成功且失败者不能归还其他会话租约()
    {
        var lease = new RichTownSessionLease();
        var documents = Enumerable.Range(0, 16).Select(_ => new RichTownDocument(At(), lease)).ToArray();
        try
        {
            Parallel.ForEach(documents, document =>
            {
                try { document.InitializeAsync(new NewDocumentActivation(""), default).GetAwaiter().GetResult(); }
                catch (InvalidOperationException) { }
            });
            var winner = Assert.Single(documents, document => document.IsInitialized);
            foreach (var loser in documents.Where(document => !document.IsInitialized)) loser.Dispose();
            winner.Play.SetActive(false);
            Assert.Throws<InvalidOperationException>(() => lease.Acquire());
            winner.Dispose(); winner.Dispose();
            using var next = lease.Acquire();
        }
        finally { foreach (var document in documents) document.Dispose(); }
    }

    [Fact]
    public async Task Restart只接受自己的身份并定向通知生命周期变化()
    {
        using var document = new RichTownDocument(At(), new());
        IWorkbenchDocumentCommandTarget target = document;
        var notifications = new List<CommandId>();
        target.CommandStateChanged += (sender, args) => { Assert.Same(document, sender); notifications.Add(args.CommandId); };
        Assert.False(target.CanExecute(RichTownDocument.RestartCommandId));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await target.ExecuteAsync(RichTownDocument.RestartCommandId, default));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await document.CaptureSaveSnapshotAsync(default));
        await document.InitializeAsync(new NewDocumentActivation(""), default);
        Assert.True(target.CanExecute(RichTownDocument.RestartCommandId));
        var other = new CommandId("myavalonia.plugin.classic.game.command.other.restart");
        Assert.False(target.CanExecute(other));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await target.ExecuteAsync(other, default));
        Assert.Throws<ArgumentNullException>(() => target.CanExecute(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await target.ExecuteAsync(null!, default));
        var before = document.Play.Snapshot;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await target.ExecuteAsync(RichTownDocument.RestartCommandId, new(true)));
        Assert.Same(before, document.Play.Snapshot);
        Roll(document);
        await target.ExecuteAsync(RichTownDocument.RestartCommandId, default);
        Assert.Equal(1, document.Play.Generation);
        Assert.Single(notifications);
        document.Dispose();
        Assert.False(target.CanExecute(RichTownDocument.RestartCommandId));
        Assert.Equal(new[] { RichTownDocument.RestartCommandId, RichTownDocument.RestartCommandId }, notifications);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await target.ExecuteAsync(RichTownDocument.RestartCommandId, default));
    }

    [Fact]
    public async Task SDK关闭信号取消保存与新命令并且迟到确认无副作用()
    {
        using var lifetime = new Lifetime();
        using var document = new RichTownDocument(At(), new(), lifetime);
        await document.InitializeAsync(new NewDocumentActivation(""), default);
        Roll(document);
        var saved = await document.CaptureSaveSnapshotAsync(default);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await document.CaptureSaveSnapshotAsync(new(true)));
        lifetime.Close();
        Assert.True(document.IsClosing);
        Assert.False(document.CanExecute(RichTownDocument.RestartCommandId));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await document.CaptureSaveSnapshotAsync(default));
        document.AcceptChanges(saved.Revision);
        Assert.True(document.IsDirty);
        document.Dispose(); document.AcceptChanges(saved.Revision);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await document.CaptureSaveSnapshotAsync(default));
    }

    [Fact]
    public async Task Host依赖注入选择公开生命周期构造且表面释放失败仍归还对局()
    {
        using var lifetime = new Lifetime();
        using var fromHost = ActivatorUtilities.CreateInstance<RichTownDocument>(new LifetimeServices(lifetime));
        lifetime.Close();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await fromHost.InitializeAsync(new NewDocumentActivation(""), default));

        var lease = new RichTownSessionLease();
        using var document = new RichTownDocument(At(), lease);
        await document.InitializeAsync(new NewDocumentActivation(""), default);
        document.AttachSurface(new BrokenSurface());
        Assert.Throws<IOException>(document.Dispose);
        Assert.True(document.Play.IsDisposed);
        using var next = lease.Acquire();
        document.Dispose();
    }

    [Fact]
    public void 保存捕获与业务提交交错时内容和修订仍配对()
    {
        var initial = At();
        var tracker = new RichTownSaveTracker(new(initial));
        tracker.Accept(new(0));
        Parallel.Invoke(
            () => { for (var i = 1; i <= 1000; i++) tracker.Observe(new(initial with { Revision = i, EventSequence = i }), 0); },
            () => { for (var i = 0; i < 1000; i++) { var captured = tracker.Capture(); Assert.Equal(captured.Revision.Value, captured.State.Snapshot.Revision); } });
        var current = tracker.Capture();
        Assert.Equal(1000, current.Revision.Value);
        Assert.False(tracker.Accept(new(999)));
        Assert.True(tracker.Accept(current.Revision));
        Assert.False(tracker.Accept(current.Revision));
    }

    private static RichTownDocument Create()
    {
        var document = new RichTownDocument(At(), new());
        document.InitializeAsync(new NewDocumentActivation(""), default).GetAwaiter().GetResult();
        return document;
    }
    private static void Roll(RichTownDocument document)
    {
        document.Play.SetActive(true);
        Assert.True(document.Play.SubmitHuman(document.Play.Stamp, new(RichTownCommandKind.Roll)));
    }
    private sealed class Lifetime : IDocumentLifetime, IDisposable
    {
        private readonly CancellationTokenSource _source = new();
        public CancellationToken ClosingToken => _source.Token;
        public bool IsClosing => _source.IsCancellationRequested;
        public void Close() => _source.Cancel();
        public void Dispose() => _source.Dispose();
    }
    private sealed class BrokenSurface : IDisposable { public void Dispose() => throw new IOException("故障释放"); }
    private sealed class LifetimeServices(IDocumentLifetime lifetime) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IDocumentLifetime) ? lifetime : null;
    }
}
