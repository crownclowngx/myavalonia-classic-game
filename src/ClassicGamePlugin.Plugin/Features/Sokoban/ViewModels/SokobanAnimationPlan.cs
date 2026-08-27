using ClassicGamePlugin.Features.Sokoban.Domain;

namespace ClassicGamePlugin.Features.Sokoban.ViewModels;

/// <summary>
/// 提供与 Avalonia 计时器无关的纯动画进度。领域移动已经提交，计划只描述如何从走前快照过渡到走后快照；
/// 因此测试可以确定验证曲线，View 被卸载时也能安全跳到最终状态。
/// </summary>
internal sealed class SokobanAnimationPlan
{
    internal static readonly TimeSpan MoveDuration = TimeSpan.FromMilliseconds(120);
    internal static readonly TimeSpan CompletionDuration = TimeSpan.FromMilliseconds(360);

    internal SokobanAnimationPlan(SokobanMoveResult move)
    {
        Move = move ?? throw new ArgumentNullException(nameof(move));
        TotalDuration = MoveDuration + (move.After.IsCompleted ? CompletionDuration : TimeSpan.Zero);
    }

    internal SokobanMoveResult Move { get; }
    internal TimeSpan TotalDuration { get; }

    internal double GetMoveProgress(TimeSpan elapsed)
    {
        var linear = Math.Clamp(elapsed.TotalMilliseconds / MoveDuration.TotalMilliseconds, 0, 1);
        return 1 - Math.Pow(1 - linear, 3);
    }

    internal double GetCompletionPulse(TimeSpan elapsed)
    {
        if (!Move.After.IsCompleted)
        {
            return 0;
        }

        var linear = Math.Clamp(
            (elapsed - MoveDuration).TotalMilliseconds / CompletionDuration.TotalMilliseconds,
            0,
            1);
        return Math.Sin(linear * Math.PI);
    }

    internal bool IsComplete(TimeSpan elapsed) => elapsed >= TotalDuration;
}
