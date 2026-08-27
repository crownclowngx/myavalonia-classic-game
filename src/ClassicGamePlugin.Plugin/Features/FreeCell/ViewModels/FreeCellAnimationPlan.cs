using ClassicGamePlugin.Features.FreeCell.Domain;

namespace ClassicGamePlugin.Features.FreeCell.ViewModels;

internal enum FreeCellAnimationStageKind
{
    Move,
    Undo,
    AutoCollect,
    Win,
}

internal readonly record struct FreeCellAnimationStage(
    FreeCellAnimationStageKind Kind,
    TimeSpan Duration);

/// <summary>
/// 已提交领域事务的纯动画描述。它不创建 DispatcherTimer，也不修改棋局；View 随时可以丢弃计划并
/// 直接显示最终快照，因此主题切换、控件卸载和设置变更不会破坏领域状态。
/// </summary>
internal sealed record FreeCellAnimationPlan(
    FreeCellTransition Transition,
    IReadOnlyList<FreeCellAnimationStage> Stages)
{
    internal TimeSpan TotalDuration =>
        TimeSpan.FromMilliseconds(Stages.Sum(stage => stage.Duration.TotalMilliseconds));

    internal static FreeCellAnimationPlan Create(FreeCellTransition transition)
    {
        var stages = new List<FreeCellAnimationStage>();
        if (transition.Kind == FreeCellActionKind.Undo)
        {
            stages.Add(new(FreeCellAnimationStageKind.Undo, TimeSpan.FromMilliseconds(140)));
        }
        else if (transition.PrimaryCardIds.Count > 0)
        {
            stages.Add(new(FreeCellAnimationStageKind.Move, TimeSpan.FromMilliseconds(140)));
        }

        if (transition.AutoCollectedCardIds.Count > 0)
        {
            stages.Add(new(FreeCellAnimationStageKind.AutoCollect, TimeSpan.FromMilliseconds(120)));
        }

        if (transition.After.State == FreeCellGameState.Won)
        {
            stages.Add(new(FreeCellAnimationStageKind.Win, TimeSpan.FromMilliseconds(250)));
        }

        return new FreeCellAnimationPlan(transition, stages);
    }
}
