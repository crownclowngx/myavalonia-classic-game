using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;
using Stride.Rendering.Compositing;
using Stride.Rendering.Lights;

namespace ClassicGamePlugin.Features.RichTown.Rendering;

/// <summary>
/// 只把画面 DTO 投影到 Stride 实体。静态棋盘和标识只建一次；房屋等级通过显示不同模型组合表现。
/// 车身颜色、车顶编号、地产归属点数共同识别玩家；禁止在此计算租金、购买或触发回合。
/// </summary>
internal sealed class RichTownBoardScene
{
    private readonly RichTownStrideGame _game;
    private readonly Scene _scene = new();
    private readonly Entity _cameraEntity;
    private readonly Entity[] _lots = new Entity[24];
    private readonly Entity[][] _houses = new Entity[24][];
    private readonly Entity[][] _owners = new Entity[24][];
    private readonly Entity[] _cars = new Entity[3];
    private readonly Entity _die;
    private readonly Entity _selection;
    private RichTownCamera _camera = RichTownCamera.Default;
    private (long Generation, long Revision) _last = (-1, -1);
    private Vector2? _press;
    private float _dragDistance;
    internal event Action<int>? CellSelected;
    private static readonly Color4[] PlayerColors = [new(0.78f, 0.18f, 0.27f, 1), new(0.11f, 0.43f, 0.87f, 1), new(0.94f, 0.51f, 0.07f, 1)];
    private static readonly Color4 White = new(0.96f, 0.96f, 0.88f, 1);
    private static readonly Color4 Ink = new(0.09f, 0.16f, 0.21f, 1);

    public RichTownBoardScene(RichTownStrideGame game)
    {
        _game = game;
        var camera = new CameraComponent { VerticalFieldOfView = 45, FarClipPlane = 100 };
        _cameraEntity = new Entity("小镇环绕相机") { camera };
        _scene.Entities.Add(_cameraEntity);
        _scene.Entities.Add(new Entity("柔和环境光") { new LightComponent { Type = new LightAmbient(), Intensity = 0.8f } });
        var sun = new Entity("日光") { new LightComponent { Type = new LightDirectional(), Intensity = 1.25f } };
        sun.Transform.Rotation = Quaternion.RotationYawPitchRoll(-0.4f, -0.9f, 0);
        _scene.Entities.Add(sun);
        Add(Box("棋盘底座", Ink, new(0, -0.25f, 0), new(12.3f, 0.45f, 12.3f)));
        Add(Box("中央公园", new(0.28f, 0.53f, 0.42f, 1), new(0, -0.02f, 0), new(8.3f, 0.2f, 8.3f)));
        Add(Box("中央步道", new(0.78f, 0.81f, 0.69f, 1), new(0, 0.09f, 0), new(7.8f, 0.025f, 0.7f)));
        Add(Box("骰子台", Ink, new(0, 0.22f, 0), new(2.2f, 0.24f, 2.2f)));
        for (var i = 0; i < 24; i++) BuildCell(i);
        for (var id = 0; id < 3; id++) { _cars[id] = CreateCar(id); Add(_cars[id]); }
        foreach (var x in new[] { -3.1f, 3.1f })
            foreach (var z in new[] { -2.7f, 2.7f })
            {
                Add(Box("树干", new(0.34f, 0.23f, 0.16f, 1), new(x, 0.38f, z), new(0.18f, 0.65f, 0.18f)));
                var foliage = Box("树冠", new(0.17f, 0.39f, 0.26f, 1), new(x, 0.9f, z), new(0.7f, 0.8f, 0.7f));
                foliage.Transform.Rotation = Quaternion.RotationY(0.4f);
                Add(foliage);
            }
        _die = CreateDie();
        Add(_die);
        _selection = new Entity("当前选择框");
        foreach (var side in new[] { -1, 1 })
        {
            Child(_selection, Box("边框", new(1, 0.83f, 0.20f, 1), new(side * 0.78f, 0.19f, 0), new(0.06f, 0.08f, 1.6f)));
            Child(_selection, Box("边框", new(1, 0.83f, 0.20f, 1), new(0, 0.19f, side * 0.78f), new(1.6f, 0.08f, 0.06f)));
        }
        Add(_selection);
        UpdateCamera();
        game.SceneSystem.SceneInstance = new SceneInstance(game.Services, _scene);
        game.SceneSystem.GraphicsCompositor = GraphicsCompositorHelper.CreateDefault(false, camera: camera, clearColor: new Color4(0.07f, 0.12f, 0.17f, 1));
    }

    private Entity Box(string name, Color4 color, Vector3 position, Vector3 scale)
    {
        var entity = new Entity(name) { new ModelComponent(_game.ColorModel(color)) };
        entity.Transform.Position = position;
        entity.Transform.Scale = scale;
        return entity;
    }
    private void Add(Entity entity) => _scene.Entities.Add(entity);
    private static void Child(Entity parent, Entity child) => parent.Transform.Children.Add(child.Transform);

    private void BuildCell(int index)
    {
        var center = RichTownBoardLayout.Cell(index);
        _lots[index] = Box($"地块 {index}", White, new(center.X, 0.05f, center.Y), new(1.53f, 0.2f, 1.53f));
        Add(_lots[index]);
        var marker = new Entity($"地块编号 {index}");
        marker.Transform.Position = new(center.X, 0.165f, center.Y - 0.54f);
        Number(marker, index, 0.16f, Ink);
        Add(marker);
        _houses[index] = new Entity[2];
        for (var building = 0; building < 2; building++)
        {
            var entity = new Entity($"{index} 号街区房屋 {building + 1}") { new ModelComponent(_game.HouseModel) { Enabled = false } };
            entity.Transform.Position = new(center.X + (building == 0 ? -0.23f : 0.27f), 0.16f, center.Y - 0.04f);
            entity.Transform.Scale = new(0.34f);
            _houses[index][building] = entity;
            Add(entity);
        }
        _owners[index] = new Entity[3];
        for (var id = 0; id < 3; id++)
        {
            var group = new Entity($"{index} 号地归属 {id + 1}");
            group.Transform.Position = new(center.X, 0.17f, center.Y + 0.62f);
            Child(group, Box("归属色条", PlayerColors[id], Vector3.Zero, new(1.15f, 0.045f, 0.17f)));
            for (var dot = 0; dot <= id; dot++) Child(group, Box("归属点数", White, new((dot - id / 2f) * 0.16f, 0.025f, 0), new(0.09f, 0.025f, 0.09f)));
            _owners[index][id] = group;
            Add(group);
            Show(group, false);
        }
    }

    private Entity CreateCar(int id)
    {
        var car = new Entity($"{id + 1} 号小汽车");
        Child(car, Box("车身", PlayerColors[id], new(0, 0.30f, 0), new(0.28f, 0.15f, 0.48f)));
        Child(car, Box("车窗", Ink, new(0, 0.42f, -0.03f), new(0.22f, 0.16f, 0.23f)));
        Child(car, Box("车顶", PlayerColors[id], new(0, 0.51f, -0.03f), new(0.24f, 0.03f, 0.26f)));
        foreach (var side in new[] { -1, 1 })
            foreach (var axle in new[] { -1, 1 }) Child(car, Box("车轮", Ink, new(side * 0.15f, 0.25f, axle * 0.16f), new(0.065f, 0.14f, 0.12f)));
        for (var pip = 0; pip <= id; pip++) Child(car, Box("车号", White, new((pip - id / 2f) * 0.068f, 0.535f, -0.03f), new(0.045f, 0.015f, 0.07f)));
        Child(car, Box("车灯", White, new(0, 0.32f, 0.245f), new(0.20f, 0.045f, 0.012f)));
        return car;
    }

    private Entity CreateDie()
    {
        var die = new Entity("确定性骰子");
        Child(die, Box("骰子", White, Vector3.Zero, new(0.9f)));
        var normals = new[] { Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitZ, -Vector3.UnitY };
        for (var face = 1; face <= 6; face++)
        {
            var normal = normals[face - 1];
            var u = MathF.Abs(normal.X) > 0 ? Vector3.UnitZ : Vector3.UnitX;
            var v = MathF.Abs(normal.Y) > 0 ? Vector3.UnitZ : Vector3.UnitY;
            foreach (var point in Pips(face))
            {
                var position = normal * 0.456f + u * point.X * 0.23f + v * point.Y * 0.23f;
                var scale = new Vector3(MathF.Abs(normal.X) > 0 ? 0.012f : 0.11f, MathF.Abs(normal.Y) > 0 ? 0.012f : 0.11f, MathF.Abs(normal.Z) > 0 ? 0.012f : 0.11f);
                Child(die, Box("骰点", Ink, position, scale));
            }
        }
        die.Transform.Position = new(0, 0.93f, 0);
        return die;
    }

    internal static IEnumerable<System.Numerics.Vector2> Pips(int value)
    {
        if (value is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(value));
        if (value is 1 or 3 or 5) yield return new(0, 0);
        if (value >= 2) { yield return new(-1, -1); yield return new(1, 1); }
        if (value >= 4) { yield return new(-1, 1); yield return new(1, -1); }
        if (value == 6) { yield return new(-1, 0); yield return new(1, 0); }
    }

    /// <summary>七段数字是程序网格，不需要字体纹理或运行时资产编译；地块始终可按编号识别。</summary>
    private void Number(Entity root, int number, float size, Color4 color)
    {
        int[] masks = [0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f];
        var text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var digit = 0; digit < text.Length; digit++)
        {
            var positions = new[] { new Vector2(0, -1), new(0.5f, -0.5f), new(0.5f, 0.5f), new(0, 1), new(-0.5f, 0.5f), new(-0.5f, -0.5f), new(0, 0) };
            for (var segment = 0; segment < 7; segment++)
                if ((masks[text[digit] - '0'] & (1 << segment)) != 0)
                {
                    var point = positions[segment];
                    var horizontal = segment is 0 or 3 or 6;
                    Child(root, Box("数字笔画", color, new((point.X + (digit - (text.Length - 1) / 2f) * 1.5f) * size, 0, point.Y * size),
                        new(horizontal ? size : size * 0.18f, 0.012f, horizontal ? size * 0.18f : size)));
                }
        }
    }

    public void Present(RichTownSceneFrame frame)
    {
        if (_last != (frame.Generation, frame.Revision))
        {
            foreach (var tile in frame.Tiles)
            {
                var color = tile.Style switch
                {
                    RichTownTileStyle.Start => new Color4(0.48f, 0.76f, 0.53f, 1), RichTownTileStyle.Chance => new(0.76f, 0.60f, 0.85f, 1),
                    RichTownTileStyle.Tax => new(0.93f, 0.60f, 0.44f, 1), RichTownTileStyle.Rest => new(0.49f, 0.77f, 0.79f, 1), _ => White
                };
                _lots[tile.Index].Get<ModelComponent>().Model = _game.ColorModel(color);
                for (var i = 0; i < 2; i++) Show(_houses[tile.Index][i], tile.OwnerId is not null && tile.Level > i);
                for (var id = 0; id < 3; id++) Show(_owners[tile.Index][id], tile.OwnerId == id);
            }
            _last = (frame.Generation, frame.Revision);
        }
        foreach (var token in frame.Tokens)
        {
            _cars[token.PlayerId].Transform.Position = new(token.Position.X, 0, token.Position.Y);
            _cars[token.PlayerId].Transform.Rotation = Quaternion.RotationY(token.Heading);
            Show(_cars[token.PlayerId], !token.IsEliminated);
        }
        var selected = RichTownBoardLayout.Cell(frame.SelectedCell);
        _selection.Transform.Position = new(selected.X, 0, selected.Y);
        var orientation = RichTownBoardLayout.DieOrientation(frame.DieValue);
        _die.Transform.Rotation = new Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W) *
            Quaternion.RotationYawPitchRoll(frame.DieSpin, frame.DieSpin * 0.6f, 0);
        _die.Transform.Position = new(0, 0.93f + MathF.Abs(MathF.Sin(frame.DieSpin)) * 0.22f, 0);
    }

    private static void Show(Entity entity, bool visible)
    {
        if (entity.Get<ModelComponent>() is { } model) model.Enabled = visible;
        foreach (var child in entity.Transform.Children) Show(child.Entity, visible);
    }

    public void ResetCamera() { _camera = RichTownCamera.Default; UpdateCamera(); }
    public void Orbit(float x, float y) { _camera = _camera.Orbit(x, y); UpdateCamera(); }
    public void Zoom(float steps) { _camera = _camera.Zoom(steps); UpdateCamera(); }
    private void UpdateCamera()
    {
        var position = _camera.Position;
        _cameraEntity.Transform.Position = new(position.X, position.Y, position.Z);
        _cameraEntity.Transform.Rotation = Quaternion.RotationYawPitchRoll(_camera.Yaw, -_camera.Pitch, 0);
    }

    public void UpdateInput(InputManager input, bool enabled)
    {
        if (!enabled || !input.HasMouse) { _press = null; return; }
        var pointer = input.MousePosition;
        var inside = pointer.X is >= 0 and <= 1 && pointer.Y is >= 0 and <= 1;
        if (inside && input.IsMouseButtonPressed(MouseButton.Left)) { _press = pointer; _dragDistance = 0; }
        if (_press is not null && input.IsMouseButtonDown(MouseButton.Left))
        {
            _dragDistance += input.MouseDelta.Length();
            if (_dragDistance > 0.008f) Orbit(-input.MouseDelta.X * 3, input.MouseDelta.Y * 2);
        }
        if (input.IsMouseButtonReleased(MouseButton.Left))
        {
            if (_press is not null && inside && _dragDistance <= 0.008f)
            {
                var buffer = _game.GraphicsDevice.Presenter.BackBuffer;
                if (RichTownBoardLayout.PickCell(new(pointer.X, pointer.Y), (float)buffer.Width / buffer.Height, _camera) is { } selected) CellSelected?.Invoke(selected);
            }
            _press = null;
        }
        if (inside && input.MouseWheelDelta != 0) Zoom(input.MouseWheelDelta);
    }
}
