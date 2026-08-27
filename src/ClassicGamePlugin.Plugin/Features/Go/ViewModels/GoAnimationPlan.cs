using ClassicGamePlugin.Features.Go.Domain;

namespace ClassicGamePlugin.Features.Go.ViewModels;

/// <summary>
/// 描述已经提交的一次围棋落子的纯视觉时间轴。落子先淡入，存在提子时再让被提棋组淡出；
/// 本对象不创建计时器、不修改棋局，因而进度曲线可由普通单元测试确定验证。
/// </summary>
internal sealed class GoAnimationPlan
{
    internal static readonly TimeSpan PlacementDuration = TimeSpan.FromMilliseconds(140);
    internal static readonly TimeSpan CaptureDuration = TimeSpan.FromMilliseconds(180);

    internal GoAnimationPlan(GoMoveResult move)
    {
        Move = move ?? throw new ArgumentNullException(nameof(move));
        TotalDuration = PlacementDuration +
            (move.CapturedPositions.Count > 0 ? CaptureDuration : TimeSpan.Zero);
    }

    internal GoMoveResult Move { get; }
    internal TimeSpan TotalDuration { get; }

    /// <summary>新棋子使用三次减速曲线从 0.25 缩放到完整尺寸。</summary>
    internal double GetPlacementScale(TimeSpan elapsed)
    {
        var linear = Clamp(elapsed.TotalMilliseconds / PlacementDuration.TotalMilliseconds);
        var eased = 1 - Math.Pow(1 - linear, 3);
        return 0.25 + (0.75 * eased);
    }

    internal double GetPlacementOpacity(TimeSpan elapsed) =>
        Clamp(elapsed.TotalMilliseconds / PlacementDuration.TotalMilliseconds);

    /// <summary>被提棋组在落子阶段结束后由 1 缩小至 0.35。</summary>
    internal double GetCaptureScale(TimeSpan elapsed)
    {
        var progress = GetCaptureProgress(elapsed);
        return 1 - (0.65 * progress);
    }

    internal double GetCaptureOpacity(TimeSpan elapsed) => 1 - GetCaptureProgress(elapsed);

    internal bool IsComplete(TimeSpan elapsed) => elapsed >= TotalDuration;

    private static double Clamp(double value) => Math.Clamp(value, 0, 1);

    private static double GetCaptureProgress(TimeSpan elapsed) =>
        Clamp((elapsed - PlacementDuration).TotalMilliseconds / CaptureDuration.TotalMilliseconds);
}
