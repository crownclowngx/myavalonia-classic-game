using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.RubiksCube;
using ClassicGamePlugin.Features.RubiksCube.Domain;
using ClassicGamePlugin.Features.RubiksCube.ViewModels;
using ClassicGamePlugin.Features.RubiksCube.Views;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class RubiksCubeGeometryAndDocumentTests
{
    [Fact]
    public void 六面动画角度与整数转动方向一致且终点贴纸几何一致()
    {
        var state = CubeRules.Apply(CubeRules.Solved(), CubeGame.Scramble(new Random(3)));
        foreach (var face in Enum.GetValues<CubeFace>())
            foreach (var direction in new[] { 1, -1 })
            {
                var move = new CubeMove(face, direction);
                var axis = CubeRules.Normals[(int)face];
                var animation = new CubeAnimationPlan(move);
                Assert.Equal(-direction * Math.PI / 4, animation.GetAngle(0.5), 8);
                var transform = Matrix4x4.CreateFromAxisAngle(CubeGeometry.Vector(axis), (float)animation.GetAngle(1));
                foreach (var position in CubeRules.Positions.Where(position => position.Dot(axis) == 1))
                {
                    var actual = Vector3.Transform(CubeGeometry.Vector(position), transform);
                    var expected = CubeGeometry.Vector(CubeRules.Rotate(position, axis, direction));
                    Assert.True(Vector3.Distance(actual, expected) < 0.00001);
                }
                var after = CubeRules.Apply(state, move);
                var movingFrame = CubeGeometry.CreateFrame(state, move, 1, -0.62f, 0.42f);
                var finalFrame = CubeGeometry.CreateFrame(after, null, 0, -0.62f, 0.42f);
                Assert.Equal(StickerVertices(finalFrame), StickerVertices(movingFrame));
            }
    }

    [Fact]
    public void 所有转层中间帧与不同视角尺寸均产生有限几何且不修改状态()
    {
        var state = CubeRules.Solved();
        foreach (var face in Enum.GetValues<CubeFace>())
            foreach (var progress in new[] { 0d, 0.25, 0.5, 0.75, 1 })
                foreach (var yaw in new[] { -0.62f, 1.2f, 3.4f })
                {
                    var frame = CubeGeometry.CreateFrame(state, new CubeMove(face), progress, yaw, -0.7f, new CubeVector(1, -1, 1));
                    Assert.NotEmpty(frame);
                    Assert.All(frame, polygon =>
                    {
                        Assert.True(polygon.Vertices.Length >= 3);
                        foreach (var vertex in polygon.Vertices)
                        {
                            var projected = CubeGeometry.Project(vertex, 320, 380, 0.65f);
                            Assert.True(float.IsFinite(projected.X) && float.IsFinite(projected.Y));
                        }
                    });
                }
        Assert.True(state.IsSolved);
    }

    [Fact]
    public async Task Document包装绑定标题与工作台重置仅作用于当前实例()
    {
        using var first = new RubiksCubeDocument(RubiksCubePlaybackAndViewModelTests.Create());
        using var second = new RubiksCubeDocument(RubiksCubePlaybackAndViewModelTests.Create());
        var changed = 0;
        first.PresentationChanged += (_, _) => changed++;
        await first.InitializeAsync(new NewDocumentActivation("魔方练习 A"), CancellationToken.None);
        await first.InitializeAsync(new NewDocumentActivation("魔方练习 A"), CancellationToken.None);
        Assert.Equal(1, changed);
        Assert.Equal("魔方练习 A", first.Presentation.Title);
        Assert.Equal("myavalonia.plugin.classic.game.document.rubiks-cube", PluginIds.RubiksCubeDocument.Value);
        var wrapper = new RubiksCubeDocumentView { DataContext = first };
        var view = new RubiksCubeView { DataContext = first.ViewModel };
        Dispatcher.UIThread.RunJobs();
        Assert.Same(first.ViewModel, wrapper.HostedViewModel);
        Assert.Same(first.ViewModel, view.HostedViewModel);
        first.ViewModel.TurnCommand.Execute("R");
        second.ViewModel.TurnCommand.Execute("F");
        RubiksCubePlaybackAndViewModelTests.Finish(first.ViewModel.Player);
        RubiksCubePlaybackAndViewModelTests.Finish(second.ViewModel.Player);
        await Task.WhenAll(first.ViewModel.PendingSolveTask, second.ViewModel.PendingSolveTask);
        var secondState = second.ViewModel.State;
        var target = (IWorkbenchDocumentCommandTarget)first;
        var notifications = new List<CommandId>();
        target.CommandStateChanged += (_, args) => notifications.Add(args.CommandId!);
        await target.ExecuteAsync(PluginIds.RestartRubiksCube, CancellationToken.None);
        Assert.True(first.ViewModel.State.IsSolved);
        Assert.Equal(secondState, second.ViewModel.State);
        Assert.All(notifications, identity => Assert.Equal(PluginIds.RestartRubiksCube, identity));
        Assert.Equal("魔方练习 A", first.Presentation.Title);
    }

    [Fact]
    public void 窄布局转为上下排列且恢复视角不修改魔方()
    {
        using var vm = RubiksCubePlaybackAndViewModelTests.Create();
        var view = new RubiksCubeView { DataContext = vm };
        var grid = view.FindControl<Grid>("LayoutRoot")!;
        var panel = view.FindControl<StackPanel>("TeachingPanel")!;
        view.UpdateLayoutForWidth(700);
        Assert.Single(grid.ColumnDefinitions);
        Assert.Equal(1, Grid.GetRow(panel));
        view.UpdateLayoutForWidth(1100);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.Equal(1, Grid.GetColumn(panel));
        var cube = view.FindControl<RubiksCubeControl>("CubeViewport")!;
        cube.ResetView();
        Assert.True(vm.State.IsSolved);
    }

    private static string[] StickerVertices(IReadOnlyList<CubePolygon> polygons) => polygons.Where(polygon => polygon.Color is not null)
        .SelectMany(polygon => polygon.Vertices.Select(vertex => $"{polygon.Color}:{Math.Round(vertex.X, 3):F3}:{Math.Round(vertex.Y, 3):F3}:{Math.Round(vertex.Z, 3):F3}"))
        .Distinct().Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void 转层中间帧的画家顺序与独立射线最近交点一致()
    {
        foreach (var face in Enum.GetValues<CubeFace>())
            foreach (var yaw in new[] { -0.62f, 1.2f, 3.4f })
            {
                var frame = CubeGeometry.CreateFrame(CubeRules.Solved(), new CubeMove(face), 0.5, yaw, 0.42f);
                for (var x = 135; x <= 465; x += 55)
                    for (var y = 135; y <= 465; y += 55)
                    {
                        var onPlane = new Vector3((x - 300) * 8 / 810f, -(y - 300) * 8 / 810f, 0);
                        var direction = onPlane - CubeGeometry.Camera;
                        var intersections = frame.Select(polygon => Intersect(polygon, direction)).Where(value => value is not null)
                            .Select(value => value!.Value).ToArray();
                        if (intersections.Length > 0)
                            Assert.InRange(intersections[^1] - intersections.Min(), -0.0001f, 0.0001f);
                    }
            }
    }

    /// <summary>独立按射线求深度，不复用被测平面排序，以检验真实的遮挡关系。</summary>
    private static float? Intersect(CubePolygon polygon, Vector3 direction)
    {
        var denominator = Vector3.Dot(direction, polygon.Normal);
        if (Math.Abs(denominator) < 0.00001) return null;
        var t = Vector3.Dot(polygon.Vertices[0] - CubeGeometry.Camera, polygon.Normal) / denominator;
        if (t <= 0) return null;
        var point = CubeGeometry.Camera + direction * t;
        for (var index = 0; index < polygon.Vertices.Length; index++)
        {
            var from = polygon.Vertices[index];
            var to = polygon.Vertices[(index + 1) % polygon.Vertices.Length];
            if (Vector3.Dot(Vector3.Cross(to - from, point - from), polygon.Normal) < -0.00001) return null;
        }
        return t;
    }

    [Fact]
    public void 默认缩放时所有转层中间帧完整位于视口边界内()
    {
        foreach (var face in Enum.GetValues<CubeFace>())
            foreach (var progress in new[] { 0d, 0.25, 0.5, 0.75, 1 })
                foreach (var yaw in new[] { -0.62f, 0.7f, 2.2f, 4.1f })
                    foreach (var pitch in new[] { -1.4f, 0.42f, 1.4f })
                    {
                        var frame = CubeGeometry.CreateFrame(CubeRules.Solved(), new CubeMove(face), progress, yaw, pitch);
                        foreach (var vertex in frame.SelectMany(polygon => polygon.Vertices))
                        {
                            var point = CubeGeometry.Project(vertex, 380, 380, 1);
                            Assert.InRange(point.X, 5, 375);
                            Assert.InRange(point.Y, 5, 375);
                        }
                    }
    }
}
