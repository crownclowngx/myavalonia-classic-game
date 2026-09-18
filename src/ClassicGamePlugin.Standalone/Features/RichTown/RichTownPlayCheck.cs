using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ClassicGamePlugin.Features.RichTown;
using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Rendering;
using ClassicGamePlugin.Features.RichTown.Views;

namespace ClassicGamePlugin.Standalone.Features.RichTown;

/// <summary>
/// 仅在显式 --rich-town-play-check 参数下运行的本地可见窗口检查，不随插件交付，不是 CI 或发布门禁。
/// 真实视口仍按正常计时器绘制、电脑仍按正常延迟提交；真人动作通过同一页面按钮发出。
/// 报告记录本机这一次的整局与窗口行为，不能替代真实 Host/Dock/GPU 驱动矩阵或肉眼检查画面。
/// </summary>
internal sealed class RichTownPlayCheck : IDisposable
{
    private readonly Window _window;
    private readonly RichTownDocument _document;
    private readonly RichTownDocumentView _view;
    private readonly string _report;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _elapsed = new();
    private readonly List<string> _checks = [];
    private int _stage;
    private int _wait;
    private long _frozenRevision;
    private int _humanCommands;
    private int _skips;
    private bool _finished;
    private Dictionary<string, Button>? _buttons;

    public RichTownPlayCheck(Window window, RichTownDocument document, RichTownDocumentView view, string report)
    {
        _window = window; _document = document; _view = view;
        _report = Path.GetFullPath(report);
        if (File.Exists(_report)) throw new IOException("检查报告已存在，请使用新证据目录。");
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(70), DispatcherPriority.Background, Tick);
        _timer.Stop();
    }

    public void Start() { _elapsed.Start(); _timer.Start(); }
    private void Click(string name)
    {
        var button = _buttons![name];
        Require(button.IsEffectivelyEnabled, $"按钮未启用：{name}");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private void Tick(object? sender, EventArgs args)
    {
        if (_finished) return;
        try
        {
            Require(_elapsed.Elapsed < TimeSpan.FromMinutes(6), "真实窗口检查超过六分钟。");
            var viewport = _view.GetLogicalDescendants().OfType<RichTownViewport>().SingleOrDefault();
            if (viewport is null) return;
            Require(viewport.Error is null, $"真实视口失败：{viewport.Error}");
            if (!viewport.IsRunning) return;
            _buttons ??= _view.GetLogicalDescendants().OfType<Button>().ToDictionary(button => button.Name!);
            var play = _document.Play;
            if (_stage == 0)
            {
                if (!play.IsActive) return;
                _checks.Add("真实 Stride 视口启动");
                Click("PauseButton");
                _frozenRevision = play.Snapshot.Revision;
                _wait = 5; _stage = 1;
                return;
            }
            if (_stage == 1)
            {
                Require(play.IsPaused && play.Snapshot.Revision == _frozenRevision, "暂停期间规则发生变化。");
                if (--_wait > 0) return;
                Click("PauseButton");
                _checks.Add("暂停与继续按钮");
                Click("RollButton");
                var committed = play.Snapshot;
                Require(play.IsAnimating, "掷骰未进入动画。");
                // 故意触发第二次点击；即使程序化绕过禁用样式，展示控制器也必须拒绝。
                _buttons["RollButton"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(ReferenceEquals(committed, play.Snapshot), "快速重复点击重复提交。");
                Click("SkipButton");
                Require(ReferenceEquals(committed, play.Snapshot) && !play.IsAnimating, "跳过动画重复结算。");
                Click("NewGameButton");
                Require(play.Snapshot.Revision == 0 && play.Generation == 1, "新对局没有取消旧显示状态。");
                // 上一步只验证新对局按钮。完整对局使用明确种子，避免随机新局使报告不可复现。
                play.Restart(RichTownSnapshot.Create(19));
                _checks.Add("重复点击、跳过动画与新对局");
                _stage = 2;
            }
            if (_stage == 2 && play.Snapshot.Revision >= 20)
            {
                _frozenRevision = play.Snapshot.Revision;
                _window.WindowState = WindowState.Minimized;
                _wait = 8; _stage = 3;
                return;
            }
            if (_stage == 3)
            {
                Require(play.Snapshot.Revision == _frozenRevision, "最小化期间规则发生变化。");
                if (--_wait > 0) return;
                _window.WindowState = WindowState.Normal;
                _stage = 4;
                return;
            }
            if (_stage == 4)
            {
                if (!play.IsActive) return;
                Require(play.IsPaused && play.Snapshot.Revision == _frozenRevision, "恢复窗口补跑了隐藏时间或没有显式暂停。");
                Click("PauseButton");
                _checks.Add("最小化冻结、恢复后显式继续");
                _window.Width = 800;
                _wait = 5; _stage = 5;
                return;
            }
            if (_stage == 5)
            {
                if (--_wait > 0) return;
                Require(viewport.Bounds.Width > 400 && viewport.Bounds.Height > 180, "窄布局未给视口保留有效尺寸。");
                Click("ZoomInButton"); Click("ZoomOutButton"); Click("ResetCameraButton");
                _checks.Add("800 DIP 窄布局与镜头按钮");
                _window.Width = 1200;
                _stage = 6;
            }
            if (play.IsAnimating)
            {
                // 前八次规则转换保留正常动画；之后使用真实跳过按钮缩短检查时长，不能改变已提交状态。
                if (play.Snapshot.Revision > 8) { Click("SkipButton"); _skips++; }
                return;
            }
            if (play.Snapshot.Phase == RichTownPhase.Finished)
            {
                Require(_stage == 6 && _humanCommands > 0, "没有完成预定窗口检查或真人操作。");
                Require(play.Snapshot.Players[0].IsComputer == false, "检查错误地把真人改成全电脑。");
                Require(_view.GetLogicalDescendants().OfType<TextBlock>().Any(text => text.IsEffectivelyVisible && text.Text?.StartsWith("最终排名", StringComparison.Ordinal) == true), "页面没有显示最终排名。");
                _checks.Add("一真人两电脑完成整局并显示排名");
                Complete(null);
                return;
            }
            if (!play.CanInteract) return;
            var snapshot = play.Snapshot;
            var planning = snapshot with { Players = snapshot.Players.SetItem(0, snapshot.Players[0] with { IsComputer = true }) };
            var command = RichTownComputerPlayer.Decide(planning)!.Command;
            if (command.PropertyIndex is { } index) _view.GetLogicalDescendants().OfType<ComboBox>().Single().SelectedIndex = index;
            var name = command.Kind switch
            {
                RichTownCommandKind.Roll => "RollButton", RichTownCommandKind.Buy => "BuyButton", RichTownCommandKind.Decline => "DeclineButton",
                RichTownCommandKind.Upgrade => "UpgradeButton", RichTownCommandKind.Sell => "SellButton", _ => "EndTurnButton"
            };
            Click(name);
            Require(play.Snapshot.Revision == snapshot.Revision + 1, "真人按钮没有通过唯一会话提交。");
            _humanCommands++;
        }
        catch (Exception error) { Complete(error.ToString()); }
    }

    private void Complete(string? error)
    {
        _finished = true;
        _timer.Stop();
        var snapshot = _document.Play.Snapshot;
        _window.Close();
        var closed = _document.Play.IsDisposed;
        var report = new { passed = error is null && closed, error, seed = 19, humanPlayers = 1, computerPlayers = 2,
            revision = snapshot.Revision, round = snapshot.Round, phase = snapshot.Phase.ToString(), humanCommands = _humanCommands,
            skippedAnimations = _skips, standings = snapshot.GetStandings(), checks = _checks, documentDisposed = closed,
            seconds = Math.Round(_elapsed.Elapsed.TotalSeconds, 2), environment = "Standalone Debug; actual Stride viewport; not Host acceptance" };
        File.WriteAllText(_report, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        if (error is not null) Environment.ExitCode = 1;
    }

    public void Dispose() { _timer.Stop(); _timer.Tick -= Tick; }
}
