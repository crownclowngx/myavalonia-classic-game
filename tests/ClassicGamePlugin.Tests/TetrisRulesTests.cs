using ClassicGamePlugin.Features.Tetris.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class TetrisRulesTests
{
    [Fact]
    public void 七种方块的四个朝向始终由四个不重复格子组成()
    {
        foreach (var type in Enum.GetValues<TetrominoType>())
        {
            foreach (var rotation in Enum.GetValues<TetrisRotation>())
            {
                var cells = TetrisRules.GetCells(new TetrisPiece(type, rotation, 0, 0));
                Assert.Equal(4, cells.Count);
                Assert.Equal(4, cells.Distinct().Count());
            }
        }
    }

    [Fact]
    public void 所有生成方块都位于十列和四行隐藏区内()
    {
        foreach (var type in Enum.GetValues<TetrominoType>())
        {
            var cells = TetrisRules.GetCells(TetrisRules.CreateSpawnPiece(type));
            Assert.All(cells, cell =>
            {
                Assert.InRange(cell.Row, 0, TetrisRules.HiddenRows - 1);
                Assert.InRange(cell.Column, 0, TetrisRules.BoardWidth - 1);
            });
        }
    }

    [Fact]
    public void SRS为八种顺逆时针转换提供五级踢墙且O不位移()
    {
        foreach (var from in Enum.GetValues<TetrisRotation>())
        {
            foreach (var clockwise in new[] { false, true })
            {
                var to = TetrisRules.Rotate(from, clockwise);
                Assert.Equal(5, TetrisRules.GetKickTests(TetrominoType.T, from, to).Count);
                Assert.Equal(5, TetrisRules.GetKickTests(TetrominoType.I, from, to).Count);
                Assert.Equal([(0, 0)], TetrisRules.GetKickTests(TetrominoType.O, from, to));
            }
        }
    }

    [Fact]
    public void 顺逆时针旋转互为逆操作()
    {
        foreach (var rotation in Enum.GetValues<TetrisRotation>())
        {
            Assert.Equal(rotation, TetrisRules.Rotate(TetrisRules.Rotate(rotation, true), false));
        }
    }
}

