using ClassicGamePlugin.Features.Tetris.Domain;

namespace ClassicGamePlugin.Features.Tetris.ViewModels;

/// <summary>
/// 描述硬降与消行的纯视觉时间轴。领域 Transition 已经提交，计划只提供确定的进度函数；View 被卸载或动画关闭时
/// 可以直接丢弃计划并显示最终状态，不需要撤销或补交领域命令。
/// </summary>
internal sealed class TetrisAnimationPlan
{
    internal static readonly TimeSpan HardDropDuration = TimeSpan.FromMilliseconds(90);
    internal static readonly TimeSpan ClearDuration = TimeSpan.FromMilliseconds(160);

    internal TetrisAnimationPlan(TetrisTransition transition)
    {
        Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        HasHardDrop = transition.DropStartRow < transition.LockedPiece.Row;
        HasLineClear = transition.ClearedRows.Count > 0;
        TotalDuration = (HasHardDrop ? HardDropDuration : TimeSpan.Zero) +
                        (HasLineClear ? ClearDuration : TimeSpan.Zero);
    }

    internal TetrisTransition Transition { get; }
    internal bool HasHardDrop { get; }
    internal bool HasLineClear { get; }
    internal TimeSpan TotalDuration { get; }
    internal TimeSpan ClearStart => HasHardDrop ? HardDropDuration : TimeSpan.Zero;

    internal double GetDropProgress(TimeSpan elapsed)
    {
        if (!HasHardDrop)
        {
            return 1;
        }

        var linear = Math.Clamp(elapsed.TotalMilliseconds / HardDropDuration.TotalMilliseconds, 0, 1);
        return 1 - Math.Pow(1 - linear, 3);
    }

    internal double GetClearProgress(TimeSpan elapsed)
    {
        if (!HasLineClear)
        {
            return 1;
        }

        return Math.Clamp(
            (elapsed - ClearStart).TotalMilliseconds / ClearDuration.TotalMilliseconds,
            0,
            1);
    }

    internal double GetClearFlash(TimeSpan elapsed) =>
        HasLineClear ? Math.Sin(GetClearProgress(elapsed) * Math.PI) : 0;

    internal bool IsComplete(TimeSpan elapsed) => elapsed >= TotalDuration;
}

