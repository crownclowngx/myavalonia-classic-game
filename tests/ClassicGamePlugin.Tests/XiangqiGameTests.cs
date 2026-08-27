using ClassicGamePlugin.Features.Xiangqi.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class XiangqiGameTests
{
    [Fact]
    public void 初始棋盘包含标准三十二子且红方先行()
    {
        var snapshot = XiangqiRules.CreateInitialSnapshot();

        Assert.Equal(XiangqiSide.Red, snapshot.CurrentSide);
        Assert.Equal(XiangqiGameState.Ready, snapshot.State);
        Assert.Equal(16, snapshot.CopyBoard().Count(piece => piece?.Side == XiangqiSide.Red));
        Assert.Equal(16, snapshot.CopyBoard().Count(piece => piece?.Side == XiangqiSide.Black));
        Assert.Equal(new XiangqiPiece(XiangqiSide.Red, XiangqiPieceType.General),
            snapshot.GetPiece(new XiangqiPosition(9, 4)));
        Assert.Equal(new XiangqiPiece(XiangqiSide.Black, XiangqiPieceType.Cannon),
            snapshot.GetPiece(new XiangqiPosition(2, 1)));
    }

    [Fact]
    public void 马腿象眼与相不过河分别产生明确非法原因()
    {
        var board = XiangqiTestFactory.EmptyBoardWithGenerals();
        XiangqiTestFactory.Set(board, 7, 2, XiangqiSide.Red, XiangqiPieceType.Horse);
        XiangqiTestFactory.Set(board, 6, 2, XiangqiSide.Red, XiangqiPieceType.Soldier);
        XiangqiTestFactory.Set(board, 9, 2, XiangqiSide.Red, XiangqiPieceType.Elephant);
        XiangqiTestFactory.Set(board, 8, 3, XiangqiSide.Black, XiangqiPieceType.Soldier);
        var snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);

        Assert.Equal(XiangqiMoveError.HorseLegBlocked,
            XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(7, 2), new(5, 3))).Error);
        Assert.Equal(XiangqiMoveError.ElephantEyeBlocked,
            XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(9, 2), new(7, 4))).Error);

        board[(8 * XiangqiRules.ColumnCount) + 3] = null;
        XiangqiTestFactory.Set(board, 5, 2, XiangqiSide.Red, XiangqiPieceType.Elephant);
        snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);
        Assert.Equal(XiangqiMoveError.ElephantCrossesRiver,
            XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(5, 2), new(3, 4))).Error);
    }

    [Fact]
    public void 炮走空路且吃子时必须恰有一个炮架()
    {
        var board = XiangqiTestFactory.EmptyBoardWithGenerals();
        XiangqiTestFactory.Set(board, 7, 1, XiangqiSide.Red, XiangqiPieceType.Cannon);
        XiangqiTestFactory.Set(board, 4, 1, XiangqiSide.Red, XiangqiPieceType.Soldier);
        XiangqiTestFactory.Set(board, 2, 1, XiangqiSide.Black, XiangqiPieceType.Horse);
        var snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);

        Assert.True(XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(7, 1), new(6, 1))).IsLegal);
        Assert.True(XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(7, 1), new(2, 1))).IsLegal);
        Assert.Equal(XiangqiMoveError.CannonScreen,
            XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(7, 1), new(3, 1))).Error);

        XiangqiTestFactory.Set(board, 3, 1, XiangqiSide.Red, XiangqiPieceType.Advisor);
        snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);
        Assert.Equal(XiangqiMoveError.CannonScreen,
            XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(7, 1), new(2, 1))).Error);
    }

    [Fact]
    public void 兵卒过河前后方向和横走规则准确()
    {
        var board = XiangqiTestFactory.EmptyBoardWithGenerals();
        XiangqiTestFactory.Set(board, 6, 0, XiangqiSide.Red, XiangqiPieceType.Soldier);
        XiangqiTestFactory.Set(board, 4, 2, XiangqiSide.Red, XiangqiPieceType.Soldier);
        var snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);

        Assert.True(XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(6, 0), new(5, 0))).IsLegal);
        Assert.Equal(XiangqiMoveError.SoldierDirection,
            XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(6, 0), new(6, 1))).Error);
        Assert.True(XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(4, 2), new(4, 3))).IsLegal);
        Assert.Equal(XiangqiMoveError.SoldierDirection,
            XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(4, 2), new(5, 2))).Error);
    }

    [Fact]
    public void 暴露将帅照面和主动暴露己方帅均被拒绝()
    {
        var board = XiangqiTestFactory.EmptyBoardWithGenerals(blockCenter: false);
        XiangqiTestFactory.Set(board, 5, 4, XiangqiSide.Red, XiangqiPieceType.Chariot);
        var snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);

        Assert.Equal(XiangqiMoveError.GeneralsFace,
            XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(5, 4), new(5, 3))).Error);

        XiangqiTestFactory.Set(board, 7, 4, XiangqiSide.Black, XiangqiPieceType.Chariot);
        XiangqiTestFactory.Set(board, 8, 4, XiangqiSide.Red, XiangqiPieceType.Advisor);
        snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);
        Assert.Equal(XiangqiMoveError.ExposesGeneral,
            XiangqiRules.ValidateMove(snapshot, new XiangqiMove(new(8, 4), new(7, 3))).Error);
    }

    [Fact]
    public void 将死与困毙均判当前走子方获胜()
    {
        var mateBoard = XiangqiTestFactory.EmptyBoardWithGenerals(blockCenter: false);
        XiangqiTestFactory.Set(mateBoard, 1, 0, XiangqiSide.Red, XiangqiPieceType.Chariot);
        XiangqiTestFactory.Set(mateBoard, 2, 4, XiangqiSide.Red, XiangqiPieceType.Chariot);
        XiangqiTestFactory.Set(mateBoard, 2, 2, XiangqiSide.Red, XiangqiPieceType.Horse);
        XiangqiTestFactory.Set(mateBoard, 2, 6, XiangqiSide.Red, XiangqiPieceType.Horse);
        var mate = XiangqiRules.TryApplyMove(
            XiangqiTestFactory.Snapshot(mateBoard, XiangqiSide.Red),
            new XiangqiMove(new(1, 0), new(1, 4)));

        Assert.NotNull(mate);
        Assert.Equal(XiangqiTerminationReason.Checkmate, mate.After.TerminationReason);
        Assert.Equal(XiangqiSide.Red, mate.After.Winner);

        var staleBoard = XiangqiTestFactory.EmptyBoardWithGenerals(blockCenter: false);
        XiangqiTestFactory.Set(staleBoard, 1, 3, XiangqiSide.Red, XiangqiPieceType.Soldier);
        XiangqiTestFactory.Set(staleBoard, 1, 5, XiangqiSide.Red, XiangqiPieceType.Soldier);
        XiangqiTestFactory.Set(staleBoard, 2, 4, XiangqiSide.Red, XiangqiPieceType.Soldier);
        XiangqiTestFactory.Set(staleBoard, 9, 0, XiangqiSide.Red, XiangqiPieceType.Chariot);
        var stale = XiangqiRules.TryApplyMove(
            XiangqiTestFactory.Snapshot(staleBoard, XiangqiSide.Red),
            new XiangqiMove(new(9, 0), new(8, 0)));

        Assert.NotNull(stale);
        Assert.Equal(XiangqiTerminationReason.Stalemate, stale.After.TerminationReason);
        Assert.Equal(XiangqiSide.Red, stale.After.Winner);
    }

    [Fact]
    public void 单方第三次长将被拒且棋盘保持不变()
    {
        var board = XiangqiTestFactory.EmptyBoardWithGenerals();
        XiangqiTestFactory.Set(board, 2, 0, XiangqiSide.Red, XiangqiPieceType.Chariot);
        var move = new XiangqiMove(new(2, 0), new(2, 4));
        var afterBoard = (XiangqiPiece?[])board.Clone();
        afterBoard[(2 * XiangqiRules.ColumnCount) + 4] = afterBoard[(2 * XiangqiRules.ColumnCount)];
        afterBoard[(2 * XiangqiRules.ColumnCount)] = null;
        var repeated = new XiangqiPositionRecord(
            XiangqiRules.ComputePositionKey(afterBoard, XiangqiSide.Black),
            XiangqiRules.CreatePositionSignature(afterBoard, XiangqiSide.Black),
            XiangqiSide.Black,
            XiangqiSide.Red,
            true);
        var current = new XiangqiPositionRecord(
            XiangqiRules.ComputePositionKey(board, XiangqiSide.Red),
            XiangqiRules.CreatePositionSignature(board, XiangqiSide.Red),
            XiangqiSide.Red,
            XiangqiSide.Black,
            false);
        var snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red,
            history: [repeated, repeated, current]);

        var validation = XiangqiRules.ValidateMove(snapshot, move);

        Assert.Equal(XiangqiMoveError.PerpetualCheck, validation.Error);
        Assert.Null(XiangqiRules.TryApplyMove(snapshot, move));
        Assert.Equal(board, snapshot.CopyBoard());
    }

    [Fact]
    public void 普通第三次重复与一百二十手未吃子自动和棋()
    {
        var board = XiangqiTestFactory.EmptyBoardWithGenerals();
        XiangqiTestFactory.Set(board, 8, 0, XiangqiSide.Red, XiangqiPieceType.Chariot);
        var move = new XiangqiMove(new(8, 0), new(8, 1));
        var afterBoard = (XiangqiPiece?[])board.Clone();
        afterBoard[(8 * XiangqiRules.ColumnCount) + 1] = afterBoard[(8 * XiangqiRules.ColumnCount)];
        afterBoard[(8 * XiangqiRules.ColumnCount)] = null;
        var repeated = new XiangqiPositionRecord(
            XiangqiRules.ComputePositionKey(afterBoard, XiangqiSide.Black),
            XiangqiRules.CreatePositionSignature(afterBoard, XiangqiSide.Black),
            XiangqiSide.Black,
            XiangqiSide.Red,
            false);
        var current = new XiangqiPositionRecord(
            XiangqiRules.ComputePositionKey(board, XiangqiSide.Red),
            XiangqiRules.CreatePositionSignature(board, XiangqiSide.Red),
            XiangqiSide.Red,
            XiangqiSide.Black,
            false);
        var repetition = XiangqiRules.TryApplyMove(
            XiangqiTestFactory.Snapshot(board, XiangqiSide.Red, history: [repeated, repeated, current]),
            move);

        Assert.Equal(XiangqiTerminationReason.ThreefoldRepetition, repetition?.After.TerminationReason);

        var limit = XiangqiRules.TryApplyMove(
            XiangqiTestFactory.Snapshot(board, XiangqiSide.Red, noCapturePlyCount: 119),
            move);
        Assert.Equal(XiangqiTerminationReason.NoCaptureLimit, limit?.After.TerminationReason);
    }

    [Fact]
    public void 非法走法不产生历史且撤销完整恢复吃子和终局前状态()
    {
        var game = new XiangqiGame();
        var before = game.CreateSnapshot();

        Assert.Null(game.Move(new XiangqiMove(new(9, 0), new(8, 1))));
        Assert.False(game.CanUndo);

        var result = game.Move(new XiangqiMove(new(6, 0), new(5, 0)));
        Assert.NotNull(result);
        Assert.True(game.CanUndo);
        game.Undo();

        Assert.Equal(before.CopyBoard(), game.CreateSnapshot().CopyBoard());
        Assert.Equal(0, game.MoveCount);
        Assert.False(game.CanUndo);
    }
}
