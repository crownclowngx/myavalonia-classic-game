using System.Collections.Immutable;
using ClassicGamePlugin.Features.RichTown.Application;
using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Rendering;

namespace ClassicGamePlugin.Features.RichTown.Presentation;

/// <summary>界面动作携带代数和修订；重开前的请求即使恰好同修订也无效。</summary>
internal readonly record struct RichTownInteractionStamp(long Generation, long Revision);

/// <summary>
/// 小镇展示会话：只协调 G2 会话、一次动画、电脑延迟和可见性，不计算经济规则。
/// 由 UI 线程串行使用；Tick 接受秒数的受控时间差，无后台任务、全局时钟或迟到动画回调。
/// Dispose/Restart 丢弃待回放状态；规则会话仍通过唯一 Submit 原子提交。
/// </summary>
internal sealed class RichTownPlayController : IDisposable
{
    private RichTownSession _session;
    private RichTownPlayback? _playback;
    private double _computerDelay;
    private bool _hasBeenActive;
    private readonly Queue<string> _log = new();
    public RichTownPlayController(RichTownSnapshot initial) { _session = new(initial); }
    public event Action? Changed;
    public RichTownSnapshot Snapshot => _session.Snapshot;
    public long Generation { get; private set; }
    public RichTownInteractionStamp Stamp => new(Generation, Snapshot.Revision);
    public bool IsAnimating => _playback is not null;
    public bool IsActive { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsDisposed { get; private set; }
    public int SelectedCell { get; private set; }
    public int LastDie { get; private set; } = 1;
    public bool HasRolled { get; private set; }
    public IReadOnlyList<string> Log => _log.ToArray();
    public bool CanInteract => !IsDisposed && IsActive && !IsPaused && !IsAnimating &&
        Snapshot.Phase != RichTownPhase.Finished && !Snapshot.Players[Snapshot.CurrentPlayerId].IsComputer;

    public RichTownSceneFrame Frame => new(Generation, Snapshot.Revision,
        RichTownBoard.Cells.Select(cell =>
        {
            var property = Snapshot.Properties.FirstOrDefault(item => item.Index == cell.Index);
            var style = cell.Kind switch
            {
                RichTownCellKind.Start => RichTownTileStyle.Start, RichTownCellKind.Property => RichTownTileStyle.Property,
                RichTownCellKind.Chance => RichTownTileStyle.Chance, RichTownCellKind.Tax => RichTownTileStyle.Tax, _ => RichTownTileStyle.Rest
            };
            return new RichTownTileVisual(cell.Index, style, property?.OwnerId, property?.Level ?? 0);
        }).ToImmutableArray(),
        Snapshot.Players.Select(player => _playback?.Pose(player) ?? new RichTownTokenVisual(player.Id,
            RichTownBoardLayout.Cell(player.Position) + RichTownBoardLayout.TokenOffset(player.Id), 0, player.IsEliminated)).ToImmutableArray(),
        Snapshot.CurrentPlayerId, SelectedCell, LastDie, _playback?.DieSpin ?? 0);

    /// <summary>首次显示自动准备；之后隐藏/最小化会暂停，恢复可见后须显式继续，不追赶墙钟时间。</summary>
    public void SetActive(bool active)
    {
        if (IsDisposed || IsActive == active) return;
        IsActive = active;
        if (!active && _hasBeenActive) IsPaused = true;
        _hasBeenActive |= active;
        _computerDelay = 0;
        Changed?.Invoke();
    }

    public void TogglePause()
    {
        if (IsDisposed || !IsActive) return;
        IsPaused = !IsPaused;
        _computerDelay = 0;
        Changed?.Invoke();
    }

    /// <summary>每次最多消耗 100 毫秒，长时间阻塞不会导致一帧内补跑多名电脑。每次只提交一条命令。</summary>
    public void Tick(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (IsDisposed || !IsActive || IsPaused) return;
        var seconds = Math.Min(elapsed.TotalSeconds, 0.1);
        if (_playback is not null)
        {
            _playback.Advance(seconds);
            if (_playback.IsComplete) { _playback = null; _computerDelay = 0; Changed?.Invoke(); }
            return;
        }
        if (Snapshot.Phase == RichTownPhase.Finished || !Snapshot.Players[Snapshot.CurrentPlayerId].IsComputer) return;
        _computerDelay += seconds;
        if (_computerDelay < 0.35) return;
        _computerDelay = 0;
        if (RichTownComputerPlayer.Decide(Snapshot) is { } request) Commit(request);
    }

    public bool SubmitHuman(RichTownInteractionStamp stamp, RichTownCommand command)
    {
        if (!CanInteract || stamp != Stamp) return false;
        return Commit(new(Snapshot.Revision, Snapshot.CurrentPlayerId, command));
    }

    private bool Commit(RichTownRequest request)
    {
        var before = Snapshot;
        var result = _session.Submit(request);
        if (!result.Accepted) return false;
        _playback = new(before, result);
        if (result.Events.OfType<RichTownRolled>().SingleOrDefault() is { } roll) { LastDie = roll.Steps; HasRolled = true; }
        if (result.Events.OfType<RichTownMoved>().SingleOrDefault() is { } move) SelectedCell = move.To;
        foreach (var item in result.Events)
            if (RichTownPresentation.EventText(item) is { } message)
            {
                _log.Enqueue(message);
                while (_log.Count > 10) _log.Dequeue();
            }
        Changed?.Invoke();
        return true;
    }

    public void SelectCell(int index)
    {
        if (IsDisposed || index is < 0 or >= RichTownBoard.CellCount || index == SelectedCell) return;
        SelectedCell = index;
        Changed?.Invoke();
    }

    public void SkipAnimation(RichTownInteractionStamp stamp)
    {
        if (IsDisposed || stamp != Stamp || _playback is null) return;
        _playback = null;
        _computerDelay = 0;
        Changed?.Invoke();
    }

    /// <summary>页面和 SDK Restart 共用此替换路径；先验证新状态，再使旧请求/动画失效。</summary>
    public void Restart(RichTownSnapshot initial) => Replace(initial, 1, false, false);

    /// <summary>恢复只显示已提交快照并保持暂停；不重播保存前事件，不补跑保存期间的时间。</summary>
    public void Restore(RichTownSnapshot snapshot, int lastDie, bool hasRolled) => Replace(snapshot, lastDie, hasRolled, true);

    private void Replace(RichTownSnapshot initial, int lastDie, bool hasRolled, bool paused)
    {
        if (IsDisposed) return;
        var next = new RichTownSession(initial);
        _session = next;
        Generation++;
        _playback = null;
        _computerDelay = 0;
        _log.Clear();
        SelectedCell = initial.Players[initial.CurrentPlayerId].Position;
        LastDie = lastDie;
        HasRolled = hasRolled;
        IsPaused = paused;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        IsActive = false;
        _playback = null;
        _computerDelay = 0;
        Changed?.Invoke();
        Changed = null;
    }
}
