using ClassicGamePlugin.Features.Xiangqi.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class XiangqiNotationTests
{
    [Fact]
    public void 标准开局着法使用红中文黑阿拉伯线路()
    {
        var initial = XiangqiRules.CreateInitialSnapshot();

        Assert.Equal("炮二平五", XiangqiNotation.Format(initial, new XiangqiMove(new(7, 1), new(7, 4))));

        var board = initial.CopyBoard();
        var black = XiangqiTestFactory.Snapshot(board, XiangqiSide.Black);
        Assert.Equal("马8进7", XiangqiNotation.Format(black, new XiangqiMove(new(0, 1), new(2, 2))));
    }

    [Fact]
    public void 直行记录步数而马仕相记录目标线路()
    {
        var initial = XiangqiRules.CreateInitialSnapshot();

        Assert.Equal("车一进一", XiangqiNotation.Format(initial, new XiangqiMove(new(9, 0), new(8, 0))));
        Assert.Equal("马二进三", XiangqiNotation.Format(initial, new XiangqiMove(new(9, 1), new(7, 2))));
        Assert.Equal("相三进五", XiangqiNotation.Format(initial, new XiangqiMove(new(9, 2), new(7, 4))));
    }

    [Fact]
    public void 同路同类棋子使用前后中与顺序编号消歧()
    {
        var board = XiangqiTestFactory.EmptyBoardWithGenerals();
        XiangqiTestFactory.Set(board, 5, 0, XiangqiSide.Red, XiangqiPieceType.Chariot);
        XiangqiTestFactory.Set(board, 8, 0, XiangqiSide.Red, XiangqiPieceType.Chariot);
        var snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);
        Assert.Equal("前车平二", XiangqiNotation.Format(snapshot, new XiangqiMove(new(5, 0), new(5, 1))));
        Assert.Equal("后车平二", XiangqiNotation.Format(snapshot, new XiangqiMove(new(8, 0), new(8, 1))));

        board = XiangqiTestFactory.EmptyBoardWithGenerals();
        foreach (var row in new[] { 1, 2, 3, 4 })
        {
            XiangqiTestFactory.Set(board, row, 0, XiangqiSide.Red, XiangqiPieceType.Soldier);
        }

        snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);
        Assert.Equal("二兵进一", XiangqiNotation.Format(snapshot, new XiangqiMove(new(2, 0), new(1, 0))));
        Assert.Equal("后兵平二", XiangqiNotation.Format(snapshot, new XiangqiMove(new(4, 0), new(4, 1))));
    }
}
