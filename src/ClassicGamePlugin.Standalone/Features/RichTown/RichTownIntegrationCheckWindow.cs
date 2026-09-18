using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ClassicGamePlugin.Features.RichTown;
using ClassicGamePlugin.Features.RichTown.Rendering;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Standalone.Features.RichTown;

/// <summary>
/// 显式本机开发检查：两次预热后反复创建/关闭 20 个真实窗口，在动画中捕获、关闭、恢复 SDK 内容。
/// 本工具不进入插件，不代替 Host 文件服务，不强制 GC，也不把一次资源曲线解释成全平台无泄漏。
/// UI 回调全程串行，异步保存使用 await，禁止等待合成线程的任务而阻塞 UI。
/// </summary>
internal sealed class RichTownIntegrationCheckWindow : Window
{
    private readonly string _report;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<object> _measurements = [];
    private readonly TextBlock _status = new() { Margin = new(20), Text = "正在执行本机小镇窗口检查…" };
    private readonly string[] _initialCaches = Caches();
    private RichTownPlayWindow? _child;
    private RichTownViewport? _viewport;
    private DocumentContent? _saved;
    private int _cycle;
    private int _stage;
    private int _wait;
    private long _hiddenSubmissions;
    private long _hiddenRevision;
    private bool _busy;
    private bool _finished;
    private readonly bool _readOnlyDirectory;

    public RichTownIntegrationCheckWindow(string report)
    {
        _report = Path.GetFullPath(report);
        if (File.Exists(_report)) throw new IOException("集成报告已存在，请使用新目录。");
        Title = "富翁小镇 · 本机开发检查"; Width = 520; Height = 130;
        Content = _status;
        _readOnlyDirectory = ProbeReadOnly();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, Tick);
        _timer.Stop();
        Opened += (_, _) => { Measure("before-warmup"); _timer.Start(); };
        Closed += (_, _) => { _timer.Stop(); _timer.Tick -= Tick; _child?.Close(); };
    }

    private static bool ProbeReadOnly()
    {
        var probe = Path.Combine(AppContext.BaseDirectory, "rich-town-write-probe-" + Guid.NewGuid().ToString("N"));
        try { File.WriteAllText(probe, "probe"); File.Delete(probe); return false; }
        catch (UnauthorizedAccessException) { return true; }
    }
    private static string[] Caches()
    {
        var root = Path.Combine(Path.GetTempPath(), "ClassicGamePlugin", "RichTown");
        return Directory.Exists(root) ? Directory.GetDirectories(root) : [];
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private void Click(string name)
    {
        var button = _child!.View.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == name);
        Require(button.IsEffectivelyEnabled, "按钮不可用：" + name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private async void Tick(object? sender, EventArgs args)
    {
        if (_busy || _finished) return;
        _busy = true;
        try
        {
            Require(_elapsed.Elapsed < TimeSpan.FromMinutes(12), "重复窗口检查超时。");
            if (_stage == 0)
            {
                _status.Text = $"预热 2 次 + 开关 20 次：{_cycle + 1} / 22";
                _child = RichTownPlayWindow.ForIntegration(_saved);
                _child.Show();
                _stage = 1;
                return;
            }
            if (_stage == 4)
            {
                if (--_wait > 0) return;
                Measure(_cycle == 2 ? "warm-baseline" : "closed");
                Require(!Caches().Except(_initialCaches).Any(), "关闭后遗留本次创建的 Stride 缓存目录。");
                if (_cycle == 22) { _wait = 30; _stage = 5; }
                else _stage = 0;
                return;
            }
            if (_stage == 5)
            {
                if (--_wait > 0) return;
                Measure("idle-after-cycles");
                Complete(null);
                return;
            }
            _viewport ??= _child!.View.GetLogicalDescendants().OfType<RichTownViewport>().SingleOrDefault();
            if (_viewport is null) return;
            Require(_viewport.Error is null, "真实视口出错：" + _viewport.Error);
            if (!_viewport.IsRunning || _viewport.RenderSubmissions < 6) return;
            var document = _child!.Document;
            if (_stage == 1)
            {
                if (!document.Play.IsActive) return;
                if (_saved is not null)
                {
                    var restored = await document.CaptureSaveSnapshotAsync(default);
                    Require(restored.Content.Payload.GetRawText() == _saved.Payload.GetRawText(), "SDK 恢复内容与关闭前不一致。");
                    Require(!document.IsDirty && document.Play.IsPaused && !document.Play.IsAnimating, "恢复没有保持干净/暂停/无旧动画。");
                }
                // 每轮从公开 Restart 开始，在掷骰动画尚未完成时保存，覆盖动画中捕获与关闭恢复。
                await document.ExecuteAsync(RichTownDocument.RestartCommandId, default);
                Click("RollButton");
                Require(document.Play.IsAnimating && document.IsDirty, "页面掷骰没有提交可保存内容。");
                var capture = await document.CaptureSaveSnapshotAsync(default);
                document.AcceptChanges(capture.Revision);
                Require(!document.IsDirty, "成功保存确认未清理脏标记。");
                _saved = capture.Content;
                _child.Width = _cycle % 2 == 0 ? 800 : 1200;
                _child.WindowState = WindowState.Minimized;
                _hiddenSubmissions = _viewport.RenderSubmissions;
                _hiddenRevision = document.Play.Snapshot.Revision;
                _wait = 4; _stage = 2;
                return;
            }
            if (_stage == 2)
            {
                Require(_viewport.RenderSubmissions == _hiddenSubmissions && document.Play.Snapshot.Revision == _hiddenRevision,
                    "最小化仍在提交绘制或规则。");
                if (--_wait > 0) return;
                _child.WindowState = WindowState.Normal;
                _wait = 3; _stage = 3;
                return;
            }
            if (_stage == 3)
            {
                if (--_wait > 0) return;
                Require(document.Play.IsPaused && document.Play.Snapshot.Revision == _hiddenRevision, "恢复窗口补跑了隐藏时间。");
                Measure("open");
                _child.Close();
                Require(document.Play.IsDisposed && !_viewport.IsRunning && _viewport.Error is null, "关闭后资源未释放或释放失败。");
                _child = null; _viewport = null;
                _cycle++; _wait = 3; _stage = 4;
            }
        }
        catch (Exception error) { Complete(error.ToString()); }
        finally { _busy = false; }
    }

    private void Measure(string label)
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        _measurements.Add(new { label, cycle = _cycle, seconds = Math.Round(_elapsed.Elapsed.TotalSeconds, 2),
            threads = process.Threads.Count, handles = process.HandleCount, privateBytes = process.PrivateMemorySize64,
            workingSetBytes = process.WorkingSet64, managedBytes = GC.GetTotalMemory(false),
            gdiObjects = GetGuiResources(process.Handle, 0), userObjects = GetGuiResources(process.Handle, 1),
            renderSubmissions = _viewport?.RenderSubmissions ?? 0, dpiScale = _child?.RenderScaling ?? RenderScaling });
    }
    private void Complete(string? error)
    {
        _finished = true; _timer.Stop();
        try { _child?.Close(); } catch (Exception closing) { error = error + "\n" + closing; }
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic).Select(assembly => assembly.Location).Order().ToArray();
        using var process = Process.GetCurrentProcess();
        var modules = process.Modules.Cast<ProcessModule>().Select(module => module.FileName).Order().ToArray();
        var report = new { passed = error is null && _cycle == 22, error, completedCycles = Math.Max(0, _cycle - 2), warmups = Math.Min(2, _cycle),
            installDirectoryReadOnly = _readOnlyDirectory, installDirectory = AppContext.BaseDirectory,
            seconds = Math.Round(_elapsed.Elapsed.TotalSeconds, 2), measurements = _measurements, loadedAssemblies = assemblies, loadedModules = modules,
            os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription,
            gpuMemory = "not measured", hostIntegration = "not verified: Build dependency policy and native viewport composition blockers",
            offlineCleanMachine = "not verified: this process does not install or download packages; OS/driver/runtime are inherited" };
        File.WriteAllText(_report, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        if (error is not null) Environment.ExitCode = 1;
        Close();
    }
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetGuiResources(nint process, uint flags);
}
