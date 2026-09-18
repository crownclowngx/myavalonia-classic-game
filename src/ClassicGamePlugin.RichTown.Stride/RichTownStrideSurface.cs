using Stride.Games;
using SdlWindow = Stride.Graphics.SDL.Window;

namespace ClassicGamePlugin.Features.RichTown.Rendering;

/// <summary>小镇专用 Stride 适配器。外部消息循环逐帧驱动，不创建线程，也不启动嵌套的 Run 循环。</summary>
internal sealed class RichTownStrideSurface(bool playable = false) : IRichTownSurface, IRichTownPlayableSurface
{
    private RichTownStrideGame? _game;
    private GameContextSDL? _context;
    private SdlWindow? _window;
    private IMessageLoop? _messages;
    private int _width;
    private int _height;

    public void Start(nint handle)
    {
        _game = new RichTownStrideGame(playable);
        _game.CellSelected += ForwardSelection;
        // SDL_CreateWindowFrom 包装的是 Avalonia 拥有的子 HWND。SDL 释放包装，Avalonia 最后销毁 HWND。
        _window = new SdlWindow("富翁小镇", handle);
        _context = new GameContextSDL(_window, 640, 480, true);
        _game.Run(_context);
        _messages = _game.Window.CreateUserManagedMessageLoop();
    }

    public void Render(int width, int height)
    {
        if (_game is null) return;
        if (_width != width || _height != height)
        {
            // DIP 在控件层按当前 TopLevel 的缩放换成物理像素；零尺寸由控制器拒绝。
            _game.GraphicsDeviceManager.PreferredBackBufferWidth = width;
            _game.GraphicsDeviceManager.PreferredBackBufferHeight = height;
            _game.GraphicsDeviceManager.ApplyChanges();
            _width = width;
            _height = height;
        }
        if (_messages?.NextFrame() == true) _context?.RunCallback?.Invoke();
    }

    public void Rotate(float radians) => _game?.RotateHouse(radians);
    public event Action<int>? CellSelected;
    private void ForwardSelection(int index) => CellSelected?.Invoke(index);
    public void Present(RichTownSceneFrame frame) => _game?.Present(frame);
    public void SetInputEnabled(bool enabled) { if (_game is not null) _game.InputEnabled = enabled; }
    public void ResetCamera() => _game?.ResetCamera();
    public void Zoom(float steps) => _game?.Zoom(steps);

    public void Dispose()
    {
        // 每步都尝试释放，前一步出错不能阻止后续 HWND 包装和游戏资源回收。
        var errors = new List<Exception>();
        void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
        Attempt(() => _messages?.Dispose());
        Attempt(() => _game?.Exit());
        Attempt(() => _context?.ExitCallback?.Invoke());
        Attempt(() => _game?.Dispose());
        Attempt(() => _window?.Dispose()); // Stride 可能尚未接管窗口；SDL 的 Dispose 本身幂等。
        _messages = null;
        _game = null;
        _context = null;
        _window = null;
        CellSelected = null;
        if (errors.Count != 0) throw new AggregateException("小镇原生资源释放失败。", errors);
    }
}
