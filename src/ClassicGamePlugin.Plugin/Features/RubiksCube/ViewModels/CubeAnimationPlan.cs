using ClassicGamePlugin.Features.RubiksCube.Domain;

namespace ClassicGamePlugin.Features.RubiksCube.ViewModels;

/// <summary>
/// 单个四分之一转的纯时间轴。角度符号与领域整数旋转一致，缓入缓出只影响画面，
/// 不决定动作是否提交，也不拥有计时器。180°必须由播放器拆成两个完整时间轴。
/// </summary>
internal sealed class CubeAnimationPlan(CubeMove move)
{
    internal static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(300);
    internal CubeMove Move { get; } = move.Turns is 1 or -1 ? move : throw new ArgumentException("时间轴只接受四分之一转。", nameof(move));
    internal double GetAngle(double progress)
    {
        var t = Math.Clamp(progress, 0, 1);
        var eased = t * t * (3 - 2 * t);
        return -Move.Turns * Math.PI / 2 * eased;
    }
}
