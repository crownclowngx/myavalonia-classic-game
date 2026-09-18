using System.Collections.Immutable;
using System.Numerics;

namespace ClassicGamePlugin.Features.RichTown.Rendering;

/// <summary>只包含显示数据的边界。渲染项目不引用 Plugin，更不能通过画面状态反向结算规则。</summary>
public enum RichTownTileStyle { Start, Property, Chance, Tax, Rest }
public sealed record RichTownTileVisual(int Index, RichTownTileStyle Style, int? OwnerId, int Level);
public sealed record RichTownTokenVisual(int PlayerId, Vector2 Position, float Heading, bool IsEliminated);

/// <summary>
/// 一帧不可变画面。Generation 区分新对局，Revision 标识已提交规则；坐标单位为场景米，Heading/DieSpin 为弧度。
/// 不传控件、领域命令或 GPU 对象；动画只改变棋子姿态和骰子旋转，不改变钱、归属或规则修订。
/// </summary>
public sealed record RichTownSceneFrame(long Generation, long Revision,
    ImmutableArray<RichTownTileVisual> Tiles, ImmutableArray<RichTownTokenVisual> Tokens,
    int CurrentPlayerId, int SelectedCell, int DieValue, float DieSpin);

/// <summary>小镇棋盘的唯一显示坐标来源：7×7 外环，四角只出现一次，0–23 连续且首尾相邻。</summary>
public static class RichTownBoardLayout
{
    public const float CellSpacing = 1.7f;
    public static Vector2 Cell(int index)
    {
        if (index is < 0 or >= 24) throw new ArgumentOutOfRangeException(nameof(index));
        var point = index switch
        {
            <= 6 => new Vector2(index - 3, 3),
            <= 12 => new Vector2(3, 9 - index),
            <= 18 => new Vector2(15 - index, -3),
            _ => new Vector2(-3, index - 21)
        };
        return point * CellSpacing;
    }

    /// <summary>同格三辆车分成三个位置，编号和颜色同时识别玩家，不因重叠而丢失棋子。</summary>
    public static Vector2 TokenOffset(int id) => id switch
    {
        0 => new(-0.38f, 0.30f), 1 => new(0, 0.30f), 2 => new(0.38f, 0.30f),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    /// <summary>按相机投影与棋盘平面求交。只响应视口内点击；中心公园、空白或无效坐标不选中地块。</summary>
    public static int? PickCell(Vector2 pointer, float aspect, RichTownCamera camera)
    {
        if (!float.IsFinite(pointer.X) || !float.IsFinite(pointer.Y) || pointer.X < 0 || pointer.X > 1 ||
            pointer.Y < 0 || pointer.Y > 1 || !float.IsFinite(aspect) || aspect <= 0) return null;
        var view = Matrix4x4.CreateLookAt(camera.Position, Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, aspect, 0.1f, 100);
        if (!Matrix4x4.Invert(view * projection, out var inverse)) return null;
        Vector3 World(float depth)
        {
            var point = Vector4.Transform(new Vector4(pointer.X * 2 - 1, 1 - pointer.Y * 2, depth, 1), inverse);
            return new Vector3(point.X, point.Y, point.Z) / point.W;
        }
        var near = World(0);
        var ray = World(1) - near;
        if (MathF.Abs(ray.Y) < 0.00001f) return null;
        var distance = (0.15f - near.Y) / ray.Y;
        if (distance < 0) return null;
        var hit = near + ray * distance;
        for (var index = 0; index < 24; index++)
        {
            var cell = Cell(index);
            if (MathF.Abs(hit.X - cell.X) <= 0.76f && MathF.Abs(hit.Z - cell.Y) <= 0.76f) return index;
        }
        return null;
    }

    /// <summary>骰子各面的确定终态。顶面为 1、前面为 2、右面为 3，反面分别为 6、5、4。</summary>
    public static Quaternion DieOrientation(int value) => value switch
    {
        1 => Quaternion.Identity,
        2 => Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2),
        3 => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2),
        4 => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathF.PI / 2),
        5 => Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2),
        6 => Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

/// <summary>可测试的相机参数。拖动和滚轮只影响画面；限制俯角和距离，避免穿板或移出棋盘。</summary>
public readonly record struct RichTownCamera(float Yaw, float Pitch, float Distance)
{
    public static RichTownCamera Default => new(0.25f, 0.88f, 20f);
    public Vector3 Position => new(MathF.Sin(Yaw) * MathF.Cos(Pitch) * Distance, MathF.Sin(Pitch) * Distance,
        MathF.Cos(Yaw) * MathF.Cos(Pitch) * Distance);
    public RichTownCamera Orbit(float horizontal, float vertical) => !float.IsFinite(horizontal) || !float.IsFinite(vertical) ? this :
        this with { Yaw = MathF.IEEERemainder(Yaw + horizontal, MathF.Tau), Pitch = Math.Clamp(Pitch + vertical, 0.45f, 1.3f) };
    public RichTownCamera Zoom(float steps) => !float.IsFinite(steps) ? this : this with { Distance = Math.Clamp(Distance - steps * 1.2f, 12, 32) };
}
