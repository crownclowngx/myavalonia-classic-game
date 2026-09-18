namespace ClassicGamePlugin.Features.RichTown;

/// <summary>
/// 小镇激活级单会话租约，区别于渲染表面租约：标签隐藏或重挂仍占有对局，只有激活失败/永久关闭才归还。
/// 租约凭唯一 token 释放，失败的第二个 Document 无法释放第一个；不限制其他游戏，也不保存业务快照。
/// </summary>
internal sealed class RichTownSessionLease
{
    internal static RichTownSessionLease Shared { get; } = new();
    private object? _owner;
    public IDisposable Acquire()
    {
        var owner = new object();
        if (Interlocked.CompareExchange(ref _owner, owner, null) is not null)
            throw new InvalidOperationException("已有一个富翁小镇对局，请先关闭该对局再新建或打开存档。");
        return new Token(this, owner);
    }
    private sealed class Token(RichTownSessionLease lease, object owner) : IDisposable
    {
        public void Dispose() => Interlocked.CompareExchange(ref lease._owner, null, owner);
    }
}
