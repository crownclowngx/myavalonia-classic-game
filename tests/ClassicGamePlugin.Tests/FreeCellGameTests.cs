using ClassicGamePlugin.Features.FreeCell.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class FreeCellGameTests
{
    [Fact]
    public void 一次移动和级联自动收牌作为一个步骤原子撤销()
    {
        var blackSeven = FreeCellTestData.Card(7, 7);
        var redEight = FreeCellTestData.Card(18, 8, FreeCellSuit.Hearts);
        var heartAce = FreeCellTestData.Card(13, 1, FreeCellSuit.Hearts);
        var snapshot = FreeCellTestData.Snapshot([[blackSeven], [redEight]], [heartAce]);
        var game = new FreeCellGame(snapshot);

        var transition = Assert.IsType<FreeCellTransition>(game.Move(
            new FreeCellMove(FreeCellLocation.Tableau(0), 0, FreeCellLocation.Tableau(1)),
            autoCollect: true));

        Assert.Equal(1, game.Current.MoveCount);
        Assert.Equal([heartAce.Id], transition.AutoCollectedCardIds);
        Assert.Equal(1, game.Current.Foundations[(int)FreeCellSuit.Hearts]);

        var undo = Assert.IsType<FreeCellTransition>(game.Undo());
        Assert.Equal(FreeCellActionKind.Undo, undo.Kind);
        Assert.Equal(0, game.Current.MoveCount);
        Assert.Equal(heartAce, game.Current.FreeCells[0]);
        Assert.Single(game.Current.Tableaus[0]);
    }

    [Fact]
    public void 最后一张K进入基础区后获胜且撤销重新开放对局()
    {
        var king = FreeCellTestData.Card(51, 13, FreeCellSuit.Diamonds);
        var snapshot = FreeCellTestData.Snapshot(
            cells: [king],
            foundations: [13, 13, 13, 12],
            moveCount: 20,
            state: FreeCellGameState.Running);
        var game = new FreeCellGame(snapshot);

        Assert.NotNull(game.Move(
            new FreeCellMove(FreeCellLocation.Cell(0), 0, FreeCellLocation.Foundation(FreeCellSuit.Diamonds)),
            autoCollect: true));
        Assert.Equal(FreeCellGameState.Won, game.Current.State);
        Assert.Equal(52, game.Current.FoundationCardCount);

        Assert.NotNull(game.Undo());
        Assert.Equal(FreeCellGameState.Running, game.Current.State);
        Assert.Equal(20, game.Current.MoveCount);
    }

    [Fact]
    public void 开启自动收牌只有实际收牌时才产生历史和步数()
    {
        var game = new FreeCellGame(FreeCellTestData.Snapshot([[FreeCellTestData.Card(0, 1)]]));

        Assert.NotNull(game.CollectSafeCards());
        Assert.Equal(1, game.Current.MoveCount);
        Assert.Null(game.CollectSafeCards());
        Assert.True(game.CanUndo);
    }
}
