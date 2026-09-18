using System.Numerics;
using System.Xml.Linq;
using ClassicGamePlugin.Features.RichTown.Rendering;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownGeometryTests
{
    [Fact]
    public void 二十四格唯一闭环且每步等距包含四角()
    {
        var points = Enumerable.Range(0, 24).Select(RichTownBoardLayout.Cell).ToArray();
        Assert.Equal(24, points.Distinct().Count());
        for (var i = 0; i < 24; i++) Assert.InRange(Vector2.Distance(points[i], points[(i + 1) % 24]), 1.699f, 1.701f);
        Assert.Equal(3, Enumerable.Range(0, 3).Select(RichTownBoardLayout.TokenOffset).Distinct().Count());
        Assert.Throws<ArgumentOutOfRangeException>(() => RichTownBoardLayout.Cell(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RichTownBoardLayout.Cell(24));
        Assert.Throws<ArgumentOutOfRangeException>(() => RichTownBoardLayout.TokenOffset(3));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void 骰子目标点数的面朝上且骰点数量正确(int value)
    {
        Vector3[] normals = [Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitZ, -Vector3.UnitY];
        var top = Vector3.Transform(normals[value - 1], RichTownBoardLayout.DieOrientation(value));
        Assert.True(Vector3.Distance(top, Vector3.UnitY) < 0.00001f);
        var pips = RichTownBoardScene.Pips(value).ToArray();
        Assert.Equal(value, pips.Length);
        Assert.Equal(value, pips.Distinct().Count());
    }

    [Fact]
    public void 镜头边界及无效输入保持稳定()
    {
        var initial = RichTownCamera.Default;
        Assert.Equal(12, initial.Zoom(100).Distance);
        Assert.Equal(32, initial.Zoom(-100).Distance);
        Assert.Equal(0.45f, initial.Orbit(0, -100).Pitch);
        Assert.Equal(1.3f, initial.Orbit(0, 100).Pitch);
        Assert.Equal(initial, initial.Zoom(float.NaN));
        Assert.Equal(initial, initial.Orbit(float.PositiveInfinity, 0));
        Assert.Equal(initial, initial.Orbit(0, float.NaN));
        Assert.True(float.IsFinite(initial.Orbit(100000, 0).Position.X));
        Assert.Throws<ArgumentOutOfRangeException>(() => RichTownBoardLayout.DieOrientation(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => RichTownBoardScene.Pips(0).ToArray());
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.6f)]
    public void 投影拾取与镜头一致且视口外与中央空地不选中(float aspect)
    {
        var camera = RichTownCamera.Default;
        var matrix = Matrix4x4.CreateLookAt(camera.Position, Vector3.Zero, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, aspect, 0.1f, 100);
        for (var index = 0; index < 24; index++)
        {
            var center = RichTownBoardLayout.Cell(index);
            var projected = Vector4.Transform(new Vector4(center.X, 0.15f, center.Y, 1), matrix);
            var pointer = new Vector2((projected.X / projected.W + 1) / 2, (1 - projected.Y / projected.W) / 2);
            Assert.Equal(index, RichTownBoardLayout.PickCell(pointer, aspect, camera));
        }
        Assert.Null(RichTownBoardLayout.PickCell(new(0.5f), aspect, camera));
        Assert.Null(RichTownBoardLayout.PickCell(new(-1, 0), aspect, camera));
        Assert.Null(RichTownBoardLayout.PickCell(new(float.NaN, 0), aspect, camera));
        Assert.Null(RichTownBoardLayout.PickCell(new(0, 2), aspect, camera));
        Assert.Null(RichTownBoardLayout.PickCell(new(0.5f), 0, camera));
        Assert.Null(RichTownBoardLayout.PickCell(new(0.5f), float.PositiveInfinity, camera));
    }

    [Fact]
    public void 独立开发程序声明支持分层子窗口且不要求管理员()
    {
        var root = Path.Combine(RichTownBoundaryTests.RepositoryRoot(), "src/ClassicGamePlugin.Standalone");
        var project = XDocument.Load(Path.Combine(root, "ClassicGamePlugin.Standalone.csproj"));
        var path = Assert.Single(project.Descendants("ApplicationManifest")).Value;
        var manifest = XDocument.Load(Path.Combine(root, path));
        Assert.Contains(manifest.Descendants(), node => node.Name.LocalName == "supportedOS" && (string?)node.Attribute("Id") == "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}");
        Assert.Contains(manifest.Descendants(), node => node.Name.LocalName == "requestedExecutionLevel" && (string?)node.Attribute("level") == "asInvoker");
    }
}
