using ClassicGamePlugin.Features.Match3.Domain;

namespace ClassicGamePlugin.Features.Match3.ViewModels;

internal enum Match3AnimationPhaseKind
{
    Swap,
    SwapBack,
    Clear,
    Fall,
    Shuffle,
    Complete,
}

internal readonly record struct Match3AnimationFrame(
    Match3AnimationPhaseKind Phase,
    int StepIndex,
    double Progress);

/// <summary>把领域 Transition 映射为固定时长的纯时间线，不依赖 Avalonia 计时器。</summary>
internal sealed class Match3AnimationPlan
{
    internal static readonly TimeSpan SwapDuration = TimeSpan.FromMilliseconds(120);
    internal static readonly TimeSpan ClearDuration = TimeSpan.FromMilliseconds(140);
    internal static readonly TimeSpan FallDuration = TimeSpan.FromMilliseconds(160);
    internal static readonly TimeSpan ShuffleDuration = TimeSpan.FromMilliseconds(220);

    internal Match3AnimationPlan(Match3TurnTransition transition)
    {
        Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        TotalDuration = transition.IsAccepted
            ? SwapDuration + TimeSpan.FromTicks(
                transition.Steps.Count * (ClearDuration + FallDuration).Ticks) +
              (transition.WasShuffled ? ShuffleDuration : TimeSpan.Zero)
            : SwapDuration + SwapDuration;
    }

    internal Match3TurnTransition Transition { get; }
    internal TimeSpan TotalDuration { get; }

    internal Match3AnimationFrame GetFrame(TimeSpan elapsed)
    {
        var remaining = elapsed;
        if (remaining < SwapDuration)
        {
            return new Match3AnimationFrame(Match3AnimationPhaseKind.Swap, -1, Ease(remaining, SwapDuration));
        }

        remaining -= SwapDuration;
        if (!Transition.IsAccepted)
        {
            return remaining < SwapDuration
                ? new Match3AnimationFrame(Match3AnimationPhaseKind.SwapBack, -1, Ease(remaining, SwapDuration))
                : new Match3AnimationFrame(Match3AnimationPhaseKind.Complete, -1, 1);
        }

        for (var index = 0; index < Transition.Steps.Count; index++)
        {
            if (remaining < ClearDuration)
            {
                return new Match3AnimationFrame(Match3AnimationPhaseKind.Clear, index, Linear(remaining, ClearDuration));
            }

            remaining -= ClearDuration;
            if (remaining < FallDuration)
            {
                return new Match3AnimationFrame(Match3AnimationPhaseKind.Fall, index, Ease(remaining, FallDuration));
            }

            remaining -= FallDuration;
        }

        if (Transition.WasShuffled && remaining < ShuffleDuration)
        {
            return new Match3AnimationFrame(Match3AnimationPhaseKind.Shuffle, -1, Ease(remaining, ShuffleDuration));
        }

        return new Match3AnimationFrame(Match3AnimationPhaseKind.Complete, -1, 1);
    }

    internal bool IsComplete(TimeSpan elapsed) => elapsed >= TotalDuration;

    private static double Linear(TimeSpan elapsed, TimeSpan duration) =>
        Math.Clamp(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);

    private static double Ease(TimeSpan elapsed, TimeSpan duration)
    {
        var linear = Linear(elapsed, duration);
        return 1 - Math.Pow(1 - linear, 3);
    }
}
