using System.Numerics;
using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Rendering;

namespace ClassicGamePlugin.Features.RichTown.Presentation;

/// <summary>
/// 仅回放一次已提交转换。事件不进入无界队列；一段结束前展示控制器不再接受下一条玩法命令。
/// 时间由调用方提供，隐藏时不推进；跳过直接丢弃本对象并显示已提交快照，不重新执行领域规则。
/// </summary>
internal sealed class RichTownPlayback(RichTownSnapshot before, RichTownTransition transition)
{
    private readonly RichTownMoved? _move = transition.Events.OfType<RichTownMoved>().SingleOrDefault();
    public double Elapsed { get; private set; }
    public double Duration => _move is not null ? 0.4 + _move.Path.Length * 0.16 + 0.16 : 0.20;
    public bool IsComplete => Elapsed >= Duration;
    public float DieSpin => _move is not null && Elapsed < 0.4 ? (float)((1 - Elapsed / 0.4) * Math.PI * 6) : 0;
    public void Advance(double seconds) => Elapsed = Math.Min(Duration, Elapsed + seconds);

    public RichTownTokenVisual Pose(RichTownPlayer player)
    {
        if (_move is null || player.Id != _move.PlayerId)
            return new(player.Id, RichTownBoardLayout.Cell(player.Position) + RichTownBoardLayout.TokenOffset(player.Id), 0, player.IsEliminated);
        var progress = Math.Clamp((Elapsed - 0.4) / 0.16, 0, _move.Path.Length);
        var completed = Math.Min((int)progress, _move.Path.Length - 1);
        var from = RichTownBoardLayout.Cell(completed == 0 ? before.Players[player.Id].Position : _move.Path[completed - 1]);
        var to = RichTownBoardLayout.Cell(_move.Path[completed]);
        var fraction = progress >= _move.Path.Length ? 1 : (float)(progress - completed);
        var position = Vector2.Lerp(from, to, fraction) + RichTownBoardLayout.TokenOffset(player.Id);
        return new(player.Id, position, MathF.Atan2(to.X - from.X, to.Y - from.Y), false);
    }
}
