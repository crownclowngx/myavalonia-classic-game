using ClassicGamePlugin.Features.ChineseCheckers.Domain;

namespace ClassicGamePlugin.Features.ChineseCheckers.ViewModels;

internal readonly record struct ChineseCheckersAnimationFrame(
    ChineseCheckersPosition From,
    ChineseCheckersPosition To,
    double Progress);

/// <summary>
/// 已提交着法的纯视觉时间轴。每段路径使用 120ms 三次减速，最后追加 160ms 到达脉冲；
/// 它不拥有计时器也不修改棋局，因此所有边界进度都能由普通单元测试验证。
/// </summary>
internal sealed class ChineseCheckersAnimationPlan
{
    internal static readonly TimeSpan SegmentDuration = TimeSpan.FromMilliseconds(120);
    internal static readonly TimeSpan ArrivalDuration = TimeSpan.FromMilliseconds(160);

    internal ChineseCheckersAnimationPlan(ChineseCheckersMoveResult move)
    {
        Move = move ?? throw new ArgumentNullException(nameof(move));
        MovementDuration = TimeSpan.FromMilliseconds(
            SegmentDuration.TotalMilliseconds * (move.Move.Path.Count - 1));
        TotalDuration = MovementDuration + ArrivalDuration;
    }

    internal ChineseCheckersMoveResult Move { get; }
    internal TimeSpan MovementDuration { get; }
    internal TimeSpan TotalDuration { get; }

    internal ChineseCheckersAnimationFrame GetMovementFrame(TimeSpan elapsed)
    {
        var segmentCount = Move.Move.Path.Count - 1;
        var raw = Math.Clamp(elapsed.TotalMilliseconds / SegmentDuration.TotalMilliseconds, 0, segmentCount);
        var index = Math.Min((int)Math.Floor(raw), segmentCount - 1);
        var linear = Math.Clamp(raw - index, 0, 1);
        var eased = 1 - Math.Pow(1 - linear, 3);
        return new ChineseCheckersAnimationFrame(
            Move.Move.Path[index],
            Move.Move.Path[index + 1],
            eased);
    }

    internal double GetArrivalScale(TimeSpan elapsed)
    {
        var progress = Math.Clamp(
            (elapsed - MovementDuration).TotalMilliseconds / ArrivalDuration.TotalMilliseconds,
            0,
            1);
        return 1 + (0.12 * Math.Sin(Math.PI * progress));
    }

    internal bool IsComplete(TimeSpan elapsed) => elapsed >= TotalDuration;
}
