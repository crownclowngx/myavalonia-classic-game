using ClassicGamePlugin.Features.SpiderSolitaire.Domain;

namespace ClassicGamePlugin.Features.SpiderSolitaire.ViewModels;

/// <summary>视图可播放的朴素动画阶段，不承担调度线程或修改棋局。</summary>
internal enum SpiderAnimationStageKind
{
    Move,
    Deal,
    Undo,
    Flip,
    CompleteRun,
    Win,
}

/// <summary>一个动画阶段及其建议持续时间。</summary>
internal readonly record struct SpiderAnimationStage(
    SpiderAnimationStageKind Kind,
    TimeSpan Duration);

/// <summary>
/// 一次领域动作对应的动画计划。阶段顺序固定为主动作、翻牌、收组、胜利；
/// View 可以中止播放并直接呈现 Transition.After，而无需回滚领域状态。
/// </summary>
internal sealed record SpiderAnimationPlan(
    SpiderGameTransition Transition,
    IReadOnlyList<SpiderAnimationStage> Stages)
{
    internal TimeSpan TotalDuration =>
        TimeSpan.FromMilliseconds(Stages.Sum(stage => stage.Duration.TotalMilliseconds));

    internal static SpiderAnimationPlan Create(SpiderGameTransition transition)
    {
        var stages = new List<SpiderAnimationStage>
        {
            transition.Kind switch
            {
                SpiderActionKind.Move => new(SpiderAnimationStageKind.Move, TimeSpan.FromMilliseconds(140)),
                SpiderActionKind.Deal => new(SpiderAnimationStageKind.Deal, TimeSpan.FromMilliseconds(300)),
                SpiderActionKind.Undo => new(SpiderAnimationStageKind.Undo, TimeSpan.FromMilliseconds(180)),
                _ => throw new InvalidOperationException("遇到了未知的蜘蛛纸牌动作类型。"),
            },
        };

        if (transition.FlippedCardIds.Count > 0)
        {
            stages.Add(new SpiderAnimationStage(SpiderAnimationStageKind.Flip, TimeSpan.FromMilliseconds(180)));
        }

        if (transition.CompletedCardIds.Count > 0)
        {
            stages.Add(new SpiderAnimationStage(SpiderAnimationStageKind.CompleteRun, TimeSpan.FromMilliseconds(400)));
        }

        if (transition.After.State == SpiderGameState.Won)
        {
            stages.Add(new SpiderAnimationStage(SpiderAnimationStageKind.Win, TimeSpan.FromMilliseconds(250)));
        }

        return new SpiderAnimationPlan(transition, stages);
    }
}
