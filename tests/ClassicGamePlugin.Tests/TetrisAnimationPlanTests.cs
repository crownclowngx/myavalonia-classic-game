using ClassicGamePlugin.Features.Tetris.Domain;
using ClassicGamePlugin.Features.Tetris.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class TetrisAnimationPlanTests
{
    [Fact]
    public void 硬降和消行按九十与一百六十毫秒顺序播放()
    {
        var transition = new TetrisTransition(
            new TetrisPiece(TetrominoType.I, TetrisRotation.Spawn, 22, 0),
            2,
            [23],
            TetrisTestBoard.Empty(),
            TetrisTestBoard.Empty(),
            TetrisSpinKind.None,
            false,
            false,
            100);
        var plan = new TetrisAnimationPlan(transition);

        Assert.Equal(TimeSpan.FromMilliseconds(250), plan.TotalDuration);
        Assert.Equal(0, plan.GetDropProgress(TimeSpan.Zero));
        Assert.Equal(1, plan.GetDropProgress(TimeSpan.FromMilliseconds(90)));
        Assert.Equal(0, plan.GetClearProgress(TimeSpan.FromMilliseconds(90)));
        Assert.Equal(1, plan.GetClearFlash(TimeSpan.FromMilliseconds(170)), 6);
        Assert.True(plan.IsComplete(TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public void 普通无消行锁定不创建阻塞动画时长()
    {
        var piece = new TetrisPiece(TetrominoType.O, TetrisRotation.Spawn, 22, 3);
        var plan = new TetrisAnimationPlan(new TetrisTransition(
            piece,
            piece.Row,
            [],
            TetrisTestBoard.Empty(),
            TetrisTestBoard.Empty(),
            TetrisSpinKind.None,
            false,
            false,
            0));

        Assert.Equal(TimeSpan.Zero, plan.TotalDuration);
        Assert.True(plan.IsComplete(TimeSpan.Zero));
    }
}

