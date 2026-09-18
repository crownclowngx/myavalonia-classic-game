using ClassicGamePlugin.Features.RichTown.Domain;

namespace ClassicGamePlugin.Features.RichTown.Application;

/// <summary>
/// 一个小镇对局的状态所有者。按实例串行提交；锁内只做纯规则计算，不通知 UI、不调用外部回调。
/// Snapshot 可在线程间读取，返回不可变值；同修订并发提交至多成功一次，拒绝仍返回当前已提交快照。
/// 构造可恢复规则快照，恢复失败不会覆盖其他会话。此类不负责 Document 重开代数、取消、文件保存或 GPU 租约。
/// </summary>
internal sealed class RichTownSession
{
    private readonly object _sync = new();
    private RichTownSnapshot _snapshot;

    public RichTownSession(RichTownSnapshot snapshot)
    {
        RichTownSnapshotValidator.Validate(snapshot);
        _snapshot = snapshot;
    }

    public RichTownSnapshot Snapshot { get { lock (_sync) return _snapshot; } }

    public RichTownTransition Submit(RichTownRequest request)
    {
        lock (_sync)
        {
            var transition = RichTownRules.Apply(_snapshot, request);
            if (transition.Accepted) _snapshot = transition.Snapshot;
            return transition;
        }
    }
}
