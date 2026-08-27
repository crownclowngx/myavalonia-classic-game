using ClassicGamePlugin.Features.Sudoku.Domain;

namespace ClassicGamePlugin.Features.Sudoku.ViewModels;

/// <summary>数独克制视觉反馈的三个阶段。</summary>
internal enum SudokuAnimationStageKind
{
    Placement,
    Conflict,
    Completion,
}

/// <summary>动画阶段与固定持续时间。</summary>
internal readonly record struct SudokuAnimationStage(SudokuAnimationStageKind Kind, TimeSpan Duration);

/// <summary>
/// 将已经提交的领域结果转换为纯时间计划。该对象不创建 DispatcherTimer、不访问控件，也不修改游戏状态。
/// </summary>
internal sealed class SudokuAnimationPlan
{
    internal static readonly TimeSpan PlacementDuration = TimeSpan.FromMilliseconds(120);
    internal static readonly TimeSpan ConflictDuration = TimeSpan.FromMilliseconds(180);
    internal static readonly TimeSpan CompletionDuration = TimeSpan.FromMilliseconds(450);

    internal SudokuAnimationPlan(SudokuMoveResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Target = result.Position;
        Conflicts = result.Conflicts;
        var stages = new List<SudokuAnimationStage>();
        if (result.Kind is SudokuMoveKind.Value or SudokuMoveKind.Hint)
        {
            stages.Add(new SudokuAnimationStage(SudokuAnimationStageKind.Placement, PlacementDuration));
        }

        if (result.Conflicts.Count > 0)
        {
            stages.Add(new SudokuAnimationStage(SudokuAnimationStageKind.Conflict, ConflictDuration));
        }

        if (result.IsCompleted)
        {
            stages.Add(new SudokuAnimationStage(SudokuAnimationStageKind.Completion, CompletionDuration));
        }

        Stages = stages.AsReadOnly();
        TotalDuration = TimeSpan.FromTicks(stages.Sum(stage => stage.Duration.Ticks));
    }

    internal SudokuPosition? Target { get; }
    internal IReadOnlySet<SudokuPosition> Conflicts { get; }
    internal IReadOnlyList<SudokuAnimationStage> Stages { get; }
    internal TimeSpan TotalDuration { get; }
    internal bool IsComplete(TimeSpan elapsed) => elapsed >= TotalDuration;

    internal double GetPlacementScale(TimeSpan elapsed)
    {
        if (!TryGetProgress(SudokuAnimationStageKind.Placement, elapsed, out var progress))
        {
            return 1;
        }

        return 0.85 + (0.15 * (1 - Math.Pow(1 - progress, 3)));
    }

    internal double GetConflictOffset(TimeSpan elapsed)
    {
        if (!TryGetProgress(SudokuAnimationStageKind.Conflict, elapsed, out var progress))
        {
            return 0;
        }

        return Math.Sin(progress * Math.PI * 6) * (1 - progress) * 6;
    }

    /// <summary>按九宫格编号依次产生短波纹强度，未轮到或已经结束的宫返回零。</summary>
    internal double GetCompletionIntensity(int boxIndex, TimeSpan elapsed)
    {
        if (!TryGetProgress(SudokuAnimationStageKind.Completion, elapsed, out var progress))
        {
            return 0;
        }

        var local = (progress * 1.8) - (boxIndex * 0.1);
        return local is <= 0 or >= 1 ? 0 : Math.Sin(local * Math.PI);
    }

    private bool TryGetProgress(
        SudokuAnimationStageKind requested,
        TimeSpan elapsed,
        out double progress)
    {
        var offset = TimeSpan.Zero;
        foreach (var stage in Stages)
        {
            if (stage.Kind == requested)
            {
                var linear = (elapsed - offset).TotalMilliseconds / stage.Duration.TotalMilliseconds;
                progress = Math.Clamp(linear, 0, 1);
                return elapsed >= offset && elapsed <= offset + stage.Duration;
            }

            offset += stage.Duration;
        }

        progress = 0;
        return false;
    }
}
