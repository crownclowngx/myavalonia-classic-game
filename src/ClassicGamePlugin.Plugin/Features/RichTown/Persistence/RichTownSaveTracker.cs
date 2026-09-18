using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.RichTown.Persistence;

/// <summary>
/// 将不可变业务内容与 Document 单调修订配对。规则 Revision 在新局回到零，本修订不会，因此旧保存确认不能清掉新局。
/// 锁只保护快照引用和数字，不包含编码、UI 事件或 GPU；捕获可与 UI 提交并行，返回的内容仍属于同一修订。
/// </summary>
internal sealed class RichTownSaveTracker(RichTownSaveState initial)
{
    private readonly object _gate = new();
    private RichTownSaveState _state = initial;
    private long _generation;
    private long _revision;
    private long _accepted;
    private long _captured = -1;
    public bool IsDirty { get { lock (_gate) return _revision != _accepted; } }

    /// <summary>初始化只在 Document 发布前调用；之后不允许重置保存修订。</summary>
    public void Initialize(RichTownSaveState state, long generation)
    {
        lock (_gate) { _state = state; _generation = generation; _revision = _accepted = 0; _captured = -1; }
    }

    public bool Observe(RichTownSaveState state, long generation)
    {
        lock (_gate)
        {
            if (_generation == generation && ReferenceEquals(_state.Snapshot, state.Snapshot)) return false;
            var wasDirty = _revision != _accepted;
            _state = state;
            _generation = generation;
            _revision = checked(_revision + 1);
            return !wasDirty;
        }
    }

    public (DocumentRevision Revision, RichTownSaveState State) Capture()
    {
        lock (_gate) { _captured = _revision; return (new(_revision), _state); }
    }

    /// <summary>只接受当前内容、并且确实捕获过的修订；重复、未来、旧局及乱序确认都不产生额外通知。</summary>
    public bool Accept(DocumentRevision revision)
    {
        lock (_gate)
        {
            if (revision.Value != _revision || revision.Value != _captured || _accepted == _revision) return false;
            _accepted = _revision;
            return true;
        }
    }
}
