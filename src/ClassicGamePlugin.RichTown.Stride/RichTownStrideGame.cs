using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering.Compositing;
using Stride.Rendering.Lights;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Buffer = Stride.Graphics.Buffer;
using Stride.Core.IO;
using Stride.Core.Storage;
using Stride.Games;

namespace ClassicGamePlugin.Features.RichTown.Rendering;

/// <summary>
/// 富翁小镇的引擎适配。拥有设备和内容缓存；G3 场景由独立渲染器构造，G1 房屋诊断入口继续保留。
/// 场景、设备与 SDL 消息均由创建本对象的 UI 线程串行使用，不启动第二个阻塞消息循环。
/// </summary>
internal sealed class RichTownStrideGame : Game
{
    private readonly List<IDisposable> _ownedResources = [];
    private FileSystemProvider? _cacheFiles;
    private DatabaseFileProvider? _databaseFiles;
    private ObjectDatabase? _database;
    private string? _cacheDirectory;
    private Entity? _house;
    private readonly bool _playable;
    private RichTownBoardScene? _board;
    private RichTownSceneFrame? _frame;
    private Model? _houseModel;
    private Buffer? _cubeBuffer;
    private readonly Dictionary<Color4, Model> _colors = [];
    internal event Action<int>? CellSelected;
    internal bool InputEnabled { get; set; }
    public RichTownStrideGame(bool playable = false)
    {
        _playable = playable;
        AutoLoadDefaultSettings = false;
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        GraphicsDeviceManager.PreferredGraphicsProfile = [GraphicsProfile.Level_11_0];
        GraphicsDeviceManager.SynchronizeWithVerticalRetrace = false;
    }

    protected override void PrepareContext()
    {
        // 引擎默认数据库位于宿主应用目录。小镇显式使用自己的用户缓存挂载点，不改变进程当前目录。
        Context.InitializeDatabase = false;
        var mount = "/rich-town-" + Guid.NewGuid().ToString("N");
        var cache = Path.Combine(Path.GetTempPath(), "ClassicGamePlugin", "RichTown", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        _cacheDirectory = cache;
        _cacheFiles = new FileSystemProvider(mount, cache);
        _database = new ObjectDatabase(mount, "index", loadDefaultBundle: false);
        _databaseFiles = new DatabaseFileProvider(_database);
        if (Services.GetService<IDatabaseFileProviderService>() is not DatabaseFileProviderService databaseService)
            throw new InvalidOperationException("Stride 数据库服务未就绪。");
        databaseService.FileProvider = _databaseFiles;
        var shaderRoot = Path.Combine(Path.GetDirectoryName(typeof(RichTownStrideGame).Assembly.Location)!, "Assets", "RichTown", "Shaders");
        foreach (var path in Directory.EnumerateFiles(shaderRoot, "*.sdsl"))
        {
            using var source = File.OpenRead(path);
            var output = _database.CreateStream();
            using (output) source.CopyTo(output);
            _database.ContentIndexMap["shaders/" + Path.GetFileName(path)] = output.CurrentHash;
        }
        base.PrepareContext();
    }

    protected override Task LoadContent()
    {
        if (_playable)
        {
            _board = new RichTownBoardScene(this);
            _board.CellSelected += index => CellSelected?.Invoke(index);
            return Task.CompletedTask;
        }
        var camera = new CameraComponent();
        var cameraEntity = new Entity("小镇相机") { camera };
        cameraEntity.Transform.Position = new Vector3(2.4f, 2.4f, 3.5f);
        cameraEntity.Transform.Rotation = Quaternion.RotationYawPitchRoll(0.6f, -0.5f, 0);
        var scene = new Scene();
        scene.Entities.Add(cameraEntity);
        scene.Entities.Add(new Entity("环境光") { new LightComponent { Type = new LightAmbient(), Intensity = 0.7f } });
        var sunlight = new Entity("日光") { new LightComponent { Type = new LightDirectional(), Intensity = 1.5f } };
        sunlight.Transform.Rotation = Quaternion.RotationYawPitchRoll(-0.5f, -0.8f, 0);
        scene.Entities.Add(sunlight);
        _house = new Entity("Kenney 房屋 A") { new ModelComponent(HouseModel) };
        scene.Entities.Add(_house);
        SceneSystem.SceneInstance = new SceneInstance(Services, scene);
        SceneSystem.GraphicsCompositor = GraphicsCompositorHelper.CreateDefault(false, camera: camera, clearColor: new Color4(0.08f, 0.13f, 0.18f, 1));
        return Task.CompletedTask;
    }

    internal Model HouseModel => _houseModel ??= CreateHouseModel();

    private Model CreateHouseModel()
    {
        var root = Path.Combine(Path.GetDirectoryName(typeof(RichTownStrideGame).Assembly.Location)!, "Assets", "RichTown");
        using var meshStream = File.OpenRead(Path.Combine(root, "house.mesh"));
        var meshData = RichTownMeshData.Read(meshStream);
        using var paletteStream = File.OpenRead(Path.Combine(root, "palette.rgba"));
        var palette = RichTownMeshData.ReadPalette(paletteStream);
        var vertices = new VertexPositionNormalTexture[meshData.VertexCount];
        for (var i = 0; i < vertices.Length; i++)
        {
            var j = i * 8;
            var a = meshData.Values;
            vertices[i] = new VertexPositionNormalTexture(new Vector3(a[j], a[j + 1], a[j + 2]),
                new Vector3(a[j + 3], a[j + 4], a[j + 5]), new Vector2(a[j + 6], a[j + 7]));
        }
        var vertexBuffer = Buffer.Vertex.New(GraphicsDevice, vertices);
        _ownedResources.Add(vertexBuffer);
        var texture = Texture.New2D(GraphicsDevice, palette.Width, palette.Height, PixelFormat.R8G8B8A8_UNorm_SRgb, palette.Pixels);
        _ownedResources.Add(texture);
        var material = Material.New(GraphicsDevice, new MaterialDescriptor
        {
            Attributes = new MaterialAttributes
            {
                Diffuse = new MaterialDiffuseMapFeature(new ComputeTextureColor(texture)),
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                CullMode = CullMode.None,
            },
        });
        var model = new Model
        {
            new Mesh
            {
                Draw = new MeshDraw
                {
                    PrimitiveType = PrimitiveType.TriangleList,
                    DrawCount = vertices.Length,
                    VertexBuffers = [new VertexBufferBinding(vertexBuffer, VertexPositionNormalTexture.Layout, vertices.Length)],
                },
            },
        };
        model.Materials.Add(material);
        return model;
    }

    /// <summary>棋盘、汽车、骰子共用一份单位立方体顶点缓冲；按颜色复用材质模型，逐帧只更新变换。</summary>
    internal Model ColorModel(Color4 color)
    {
        if (_colors.TryGetValue(color, out var cached)) return cached;
        if (_cubeBuffer is null)
        {
            var vertices = new List<VertexPositionNormalTexture>();
            void Face(Vector3 normal, Vector3 right, Vector3 up)
            {
                var a = normal * 0.5f - right * 0.5f - up * 0.5f;
                var b = normal * 0.5f + right * 0.5f - up * 0.5f;
                var c = normal * 0.5f + right * 0.5f + up * 0.5f;
                var d = normal * 0.5f - right * 0.5f + up * 0.5f;
                foreach (var point in new[] { a, b, c, a, c, d }) vertices.Add(new(point, normal, Vector2.Zero));
            }
            Face(Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ);
            Face(-Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ);
            Face(Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY);
            Face(-Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY);
            Face(Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY);
            Face(-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY);
            _cubeBuffer = Buffer.Vertex.New(GraphicsDevice, vertices.ToArray());
            _ownedResources.Add(_cubeBuffer);
        }
        var material = Material.New(GraphicsDevice, new MaterialDescriptor
        {
            Attributes = new MaterialAttributes { Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(color)), DiffuseModel = new MaterialDiffuseLambertModelFeature(), CullMode = CullMode.None }
        });
        var model = new Model { new Mesh { Draw = new MeshDraw
        {
            PrimitiveType = PrimitiveType.TriangleList, DrawCount = 36,
            VertexBuffers = [new VertexBufferBinding(_cubeBuffer, VertexPositionNormalTexture.Layout, 36)]
        } } };
        model.Materials.Add(material);
        _colors.Add(color, model);
        return model;
    }

    internal void Present(RichTownSceneFrame frame) => _frame = frame;
    internal void ResetCamera() => _board?.ResetCamera();
    internal void Zoom(float steps) => _board?.Zoom(steps);

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (_frame is { } frame) _board?.Present(frame);
        _board?.UpdateInput(Input, InputEnabled);
    }

    /// <summary>G1 的可见输入反馈；旋转显示模型，不修改任何游戏规则。</summary>
    internal void RotateHouse(float radians)
    {
        if (_board is not null) { _board.Orbit(radians, 0); return; }
        if (_house is not null)
            _house.Transform.Rotation *= Quaternion.RotationY(radians);
    }

    protected override void Destroy()
    {
        var errors = new List<Exception>();
        void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
        // 场景已停止驱动，先释放自己创建的 GPU 资源，再让 Game 销毁设备；不依赖终结器回收显存。
        foreach (var resource in _ownedResources) Attempt(resource.Dispose);
        _ownedResources.Clear();
        CellSelected = null;
        Attempt(() => base.Destroy());
        Attempt(() => _databaseFiles?.Dispose());
        Attempt(() => _database?.Dispose());
        Attempt(() => _cacheFiles?.Dispose());
        // 只清理本实例创建的随机目录，不扫描或清理其他小镇、其他游戏、宿主的缓存。
        if (_cacheDirectory is { } directory)
            Attempt(() => Directory.Delete(directory, recursive: true));
        _cacheDirectory = null;
        if (errors.Count != 0) throw new AggregateException("小镇场景释放失败。", errors);
    }
}
