using System.Numerics;
using ClassicGamePlugin.Features.RubiksCube.Domain;
using ClassicGamePlugin.Features.RubiksCube.ViewModels;

namespace ClassicGamePlugin.Features.RubiksCube.Views;

internal sealed record CubePolygon(Vector3[] Vertices, Vector3 Normal, CubeFace? Color, bool Highlight);

/// <summary>
/// 只负责三维几何与可见顺序。每个块体的六个黑色面都参与绘制，转层时露出的内部仍有实体厚度。
/// 使用平面分割的画家排序处理交叠多边形，避免仅按面中心深度排序导致转到 45°时贴纸穿透。
/// </summary>
internal static class CubeGeometry
{
    internal static readonly Vector3 Camera = new(0, 0, 8);
    internal const float Spacing = 1.04f;
    private const float HalfSize = 0.49f;
    private const float Epsilon = 0.0001f;

    internal static Vector3 Vector(CubeVector vector) => new(vector.X, vector.Y, vector.Z);
    internal static Matrix4x4 ViewMatrix(float yaw, float pitch) => Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateRotationX(pitch);

    internal static IReadOnlyList<CubePolygon> CreateFrame(CubeState state, CubeMove? move, double progress,
        float yaw, float pitch, CubeVector? target = null)
    {
        var view = ViewMatrix(yaw, pitch);
        var rotatingAxis = move is { } turn ? CubeRules.Normals[(int)turn.Face] : default;
        var rotation = move is { } active
            ? Matrix4x4.CreateFromAxisAngle(Vector(rotatingAxis), (float)new CubeAnimationPlan(active).GetAngle(progress))
            : Matrix4x4.Identity;
        var polygons = new List<CubePolygon>();
        foreach (var position in CubeRules.Positions)
        {
            var transform = move is not null && position.Dot(rotatingAxis) == 1 ? rotation * view : view;
            var center = Vector(position) * Spacing;
            foreach (var normal in CubeRules.Normals)
            {
                AddSquare(polygons, center, Vector(normal), HalfSize, HalfSize, transform, null, false);
                var slot = FindSlot(position, normal);
                if (slot < 0) continue;
                var home = CubeRules.Slots[state[slot]].Position;
                AddSquare(polygons, center, Vector(normal), HalfSize + 0.003f, 0.415f, transform,
                    state.ColorAt(slot), target == home);
            }
        }
        var ordered = new List<CubePolygon>();
        Sort(polygons, ordered);
        return ordered.AsReadOnly();
    }

    internal static Vector2 Project(Vector3 point, float width, float height, float zoom)
    {
        // 所有转层都保持顶点到原点的距离。1.35 的投影比例为最大包围球留出边距，
        // 即使近端底角在透视下变大，默认缩放也不会裁掉块体；用户放大时允许局部观察。
        var scale = Math.Min(width, height) * 1.35f * zoom / Math.Max(0.1f, Camera.Z - point.Z);
        return new Vector2(width / 2 + point.X * scale, height / 2 - point.Y * scale);
    }

    private static int FindSlot(CubeVector position, CubeVector normal)
    {
        for (var index = 0; index < CubeRules.Slots.Count; index++)
            if (CubeRules.Slots[index] == new CubeSlot(position, normal)) return index;
        return -1;
    }

    private static void AddSquare(List<CubePolygon> polygons, Vector3 center, Vector3 normal, float offset,
        float halfWidth, Matrix4x4 transform, CubeFace? color, bool highlight)
    {
        var tangent = Math.Abs(normal.Y) > 0.5f ? Vector3.UnitX : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, normal));
        var bitangent = Vector3.Cross(normal, tangent);
        center += normal * offset;
        var transformedNormal = Vector3.TransformNormal(normal, transform);
        var transformedCenter = Vector3.Transform(center, transform);
        if (Vector3.Dot(transformedNormal, Camera - transformedCenter) <= 0) return;
        Vector3[] vertices =
        [
            center - tangent * halfWidth - bitangent * halfWidth,
            center + tangent * halfWidth - bitangent * halfWidth,
            center + tangent * halfWidth + bitangent * halfWidth,
            center - tangent * halfWidth + bitangent * halfWidth,
        ];
        polygons.Add(new CubePolygon(vertices.Select(vertex => Vector3.Transform(vertex, transform)).ToArray(), transformedNormal, color, highlight));
    }

    /// <summary>
    /// 用已有面作分割平面，把跨面的多边形切成前后两部分再由远到近绘制。这里只生成当前帧的
    /// 小型列表，不缓存控件或画刷；分割后的几何保留原贴纸颜色与目标标记。
    /// </summary>
    private static void Sort(List<CubePolygon> polygons, List<CubePolygon> ordered)
    {
        if (polygons.Count == 0) return;
        var plane = polygons[polygons.Count / 2];
        var origin = plane.Vertices[0];
        var front = new List<CubePolygon>();
        var back = new List<CubePolygon>();
        var coplanar = new List<CubePolygon>();
        foreach (var polygon in polygons)
        {
            var distances = polygon.Vertices.Select(vertex => Vector3.Dot(vertex - origin, plane.Normal)).ToArray();
            var positive = distances.Any(distance => distance > Epsilon);
            var negative = distances.Any(distance => distance < -Epsilon);
            if (!positive && !negative) coplanar.Add(polygon);
            else if (!negative) front.Add(polygon);
            else if (!positive) back.Add(polygon);
            else
            {
                AddClipped(polygon, distances, true, front);
                AddClipped(polygon, distances, false, back);
            }
        }
        var cameraInFront = Vector3.Dot(Camera - origin, plane.Normal) >= 0;
        Sort(cameraInFront ? back : front, ordered);
        ordered.AddRange(coplanar);
        Sort(cameraInFront ? front : back, ordered);
    }

    private static void AddClipped(CubePolygon polygon, float[] distances, bool front, List<CubePolygon> output)
    {
        var vertices = new List<Vector3>();
        for (var index = 0; index < polygon.Vertices.Length; index++)
        {
            var next = (index + 1) % polygon.Vertices.Length;
            var inside = front ? distances[index] >= 0 : distances[index] <= 0;
            var nextInside = front ? distances[next] >= 0 : distances[next] <= 0;
            if (inside) vertices.Add(polygon.Vertices[index]);
            if (inside != nextInside)
            {
                var t = distances[index] / (distances[index] - distances[next]);
                vertices.Add(Vector3.Lerp(polygon.Vertices[index], polygon.Vertices[next], t));
            }
        }
        if (vertices.Count >= 3) output.Add(polygon with { Vertices = vertices.ToArray() });
    }
}
