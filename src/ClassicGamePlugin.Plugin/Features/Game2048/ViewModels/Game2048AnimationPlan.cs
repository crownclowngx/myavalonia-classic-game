using ClassicGamePlugin.Features.Game2048.Domain;

namespace ClassicGamePlugin.Features.Game2048.ViewModels;

/// <summary>2048 一次有效移动的两个朴素视觉阶段。</summary>
internal enum Game2048AnimationStageKind
{
    Slide,
    Feedback,
}

/// <summary>动画阶段及其固定建议持续时间。</summary>
internal readonly record struct Game2048AnimationStage(
    Game2048AnimationStageKind Kind,
    TimeSpan Duration);

/// <summary>
/// 把已经提交的领域 Transition 转换为 View 可回放的固定节奏。计划只提供时间和纯进度计算，
/// 不拥有 DispatcherTimer、不修改棋盘，也不依赖真实墙钟。
/// </summary>
internal sealed class Game2048AnimationPlan
{
    internal static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(110);
    internal static readonly TimeSpan FeedbackDuration = TimeSpan.FromMilliseconds(90);

    internal Game2048AnimationPlan(Game2048Transition transition)
    {
        Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        Stages = Array.AsReadOnly(
        [
            new Game2048AnimationStage(Game2048AnimationStageKind.Slide, SlideDuration),
            new Game2048AnimationStage(Game2048AnimationStageKind.Feedback, FeedbackDuration),
        ]);
    }

    internal Game2048Transition Transition { get; }
    internal IReadOnlyList<Game2048AnimationStage> Stages { get; }
    internal TimeSpan TotalDuration => SlideDuration + FeedbackDuration;

    /// <summary>获取使用三次减速缓动的滑动进度。</summary>
    internal double GetSlideProgress(TimeSpan elapsed)
    {
        var linear = Clamp(elapsed.TotalMilliseconds / SlideDuration.TotalMilliseconds);
        return 1 - Math.Pow(1 - linear, 3);
    }

    /// <summary>获取滑动完成后反馈阶段的线性进度。</summary>
    internal double GetFeedbackProgress(TimeSpan elapsed) =>
        Clamp((elapsed - SlideDuration).TotalMilliseconds / FeedbackDuration.TotalMilliseconds);

    /// <summary>合并方块在反馈阶段从 1 放大到 1.12，再平滑回到 1。</summary>
    internal double GetMergeScale(TimeSpan elapsed)
    {
        var progress = GetFeedbackProgress(elapsed);
        return progress <= 0.5
            ? 1 + (0.12 * progress / 0.5)
            : 1.12 - (0.12 * (progress - 0.5) / 0.5);
    }

    /// <summary>新生方块在反馈阶段使用减速曲线从零缩放到完整尺寸。</summary>
    internal double GetSpawnScale(TimeSpan elapsed)
    {
        var linear = GetFeedbackProgress(elapsed);
        return 1 - Math.Pow(1 - linear, 3);
    }

    internal bool IsComplete(TimeSpan elapsed) => elapsed >= TotalDuration;

    private static double Clamp(double value) => Math.Clamp(value, 0, 1);
}
