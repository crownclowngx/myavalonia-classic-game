using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Presentation;
using ClassicGamePlugin.Features.RichTown.Rendering;

namespace ClassicGamePlugin.Features.RichTown.Views;

/// <summary>
/// 中文玩法页面，仅负责布局、输入路由和只读投影。规则由 G2 会话处理，动画/电脑节奏由展示控制器处理。
/// 控件处于视口外，不依赖原生子窗口上方的 Avalonia 叠层；G1 的真实宿主合成阻塞仍需独立验证。
/// 每次绑定建立一份可释放租约，Document 替换 View 时同时退订事件、停止帧循环，不能留下旧页面回调。
/// </summary>
public sealed class RichTownDocumentView : UserControl
{
    private static readonly IBrush Ink = Brush.Parse("#182E3B");
    private static readonly IBrush Muted = Brush.Parse("#58707C");
    private static readonly string[] PlayerColors = ["#C73249", "#246CCB", "#CF770F"];
    private readonly Grid _body = new() { ColumnDefinitions = new("*,300"), ColumnSpacing = 16, RowSpacing = 12 };
    private readonly Border _stage = new() { Background = Brush.Parse("#12232E"), Padding = new Thickness(0) };
    private readonly ScrollViewer _sidebar = new() { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    private readonly TextBlock _round = Text("第 1 / 30 轮", 15);
    private readonly TextBlock _status = Text("正在准备棋盘…", 16);
    private readonly TextBlock _surfaceStatus = Text("正在加载小镇…", 12);
    private readonly TextBlock _die = Text("掷骰出发", 16);
    private readonly TextBlock[] _players = new TextBlock[3];
    private readonly TextBlock _details = Text("", 14);
    private readonly TextBlock _log = Text("欢迎来到小镇。购买街区，升级房屋，积累你的资产。", 12);
    private readonly TextBlock _ranking = Text("", 14);
    private readonly ComboBox _cells = new() { Name = "CellSelector", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = Enumerable.Range(0, 24).Select(index => $"{index:00} · {RichTownPresentation.Cell(index)}").ToArray() };
    private readonly Dictionary<RichTownCommandKind, Button> _actions = [];
    private readonly Button _pause;
    private readonly Button _skip;
    private readonly Button _newGame;
    private RichTownDocument? _document;
    private RichTownViewport? _viewport;
    private BindingLease? _binding;
    private bool _refreshing;

    public RichTownDocumentView()
    {
        Background = Brush.Parse("#EDF2EF");
        var root = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(18), RowSpacing = 14 };
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        var heading = new StackPanel { Spacing = 3 };
        heading.Children.Add(Text("富翁小镇", 27, FontWeight.Bold));
        heading.Children.Add(Text("一座小镇 · 三位玩家 · 三十轮", 12, color: Muted));
        header.Children.Add(heading);
        var tools = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        _pause = Button("暂停", "PauseButton", () => _document?.Play.TogglePause());
        _skip = Button("跳过动画", "SkipButton", () => { if (_document is { } doc) doc.Play.SkipAnimation(doc.Play.Stamp); });
        _newGame = Button("新对局", "NewGameButton", () => _document?.NewGame());
        tools.Children.Add(_pause); tools.Children.Add(_skip); tools.Children.Add(_newGame);
        Grid.SetColumn(tools, 1); header.Children.Add(tools);
        root.Children.Add(header);

        var field = new Grid { RowDefinitions = new("Auto,*,Auto"), RowSpacing = 8, MinHeight = 220 };
        var sceneHeader = new Grid { ColumnDefinitions = new("*,Auto") };
        _round.FontWeight = FontWeight.Bold;
        sceneHeader.Children.Add(_round);
        var camera = new WrapPanel();
        camera.Children.Add(Button("−", "ZoomOutButton", () => _viewport?.Zoom(-1)));
        camera.Children.Add(Button("＋", "ZoomInButton", () => _viewport?.Zoom(1)));
        camera.Children.Add(Button("复位镜头", "ResetCameraButton", () => _viewport?.ResetCamera()));
        camera.Children.Add(Button("重试画面", "RetryViewportButton", () => _viewport?.RestartSurface()));
        Grid.SetColumn(camera, 1); sceneHeader.Children.Add(camera);
        field.Children.Add(sceneHeader);
        Grid.SetRow(_stage, 1); field.Children.Add(_stage);
        var help = new StackPanel { Spacing = 3 };
        help.Children.Add(Text("拖动棋盘旋转 · 滚轮缩放 · 点击地块查看详情", 12, color: Muted));
        _surfaceStatus.Foreground = Muted;
        help.Children.Add(_surfaceStatus);
        Grid.SetRow(help, 2); field.Children.Add(help);
        _body.Children.Add(field);

        var side = new StackPanel { Spacing = 12 };
        side.Children.Add(_die);
        for (var id = 0; id < 3; id++)
        {
            _players[id] = Text("", 14);
            side.Children.Add(new Border { Background = Brushes.White, BorderBrush = Brush.Parse(PlayerColors[id]), BorderThickness = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(12, 9), CornerRadius = new CornerRadius(7), Child = _players[id] });
        }
        side.Children.Add(Text("街区详情", 16, FontWeight.Bold));
        side.Children.Add(_cells);
        side.Children.Add(_details);
        side.Children.Add(_ranking);
        side.Children.Add(Text("小镇动态", 15, FontWeight.Bold));
        _log.Foreground = Muted;
        side.Children.Add(_log);
        _sidebar.Content = side;
        Grid.SetColumn(_sidebar, 1); _body.Children.Add(_sidebar);
        Grid.SetRow(_body, 1); root.Children.Add(_body);

        var footer = new StackPanel { Spacing = 9 };
        footer.Children.Add(_status);
        var actions = new WrapPanel();
        AddAction(actions, RichTownCommandKind.Roll, "掷骰出发", "RollButton");
        AddAction(actions, RichTownCommandKind.Buy, "购买街区", "BuyButton");
        AddAction(actions, RichTownCommandKind.Decline, "放弃购买", "DeclineButton");
        AddAction(actions, RichTownCommandKind.Upgrade, "升级选中地产", "UpgradeButton");
        AddAction(actions, RichTownCommandKind.Sell, "出售选中地产", "SellButton");
        AddAction(actions, RichTownCommandKind.EndTurn, "结束回合", "EndTurnButton");
        _actions[RichTownCommandKind.Roll].Background = Brush.Parse("#24644F");
        _actions[RichTownCommandKind.Roll].Foreground = Brushes.White;
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Light, Child = root };
        _cells.SelectionChanged += (_, _) => { if (!_refreshing && _cells.SelectedIndex >= 0) _document?.Play.SelectCell(_cells.SelectedIndex); };
        SizeChanged += (_, _) => UpdateLayoutMode();
        DataContextChanged += (_, _) => BindDocument();
    }

    private static TextBlock Text(string text, double size, FontWeight? weight = null, IBrush? color = null) => new()
    {
        Text = text, FontSize = size, FontWeight = weight ?? FontWeight.Normal, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap
    };
    private static Button Button(string text, string name, Action action)
    {
        var button = new Button { Name = name, Content = text, Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 6, 4), MinHeight = 34 };
        button.Click += (_, _) => action();
        return button;
    }

    private void AddAction(Panel panel, RichTownCommandKind kind, string text, string name)
    {
        var button = Button(text, name, () =>
        {
            if (_document is { } doc && _actions[kind].Tag is HumanAction action) doc.Play.SubmitHuman(action.Stamp, action.Command);
        });
        _actions.Add(kind, button);
        panel.Children.Add(button);
    }

    private void BindDocument()
    {
        if (ReferenceEquals(DataContext, _document)) return;
        _binding?.Dispose();
        _binding = null;
        _document = DataContext as RichTownDocument;
        _stage.Child = null;
        _viewport = null;
        if (_document is not { IsInitialized: true } doc) return;
        var viewport = new RichTownViewport();
        _viewport = viewport;
        void Frame(TimeSpan elapsed, bool active)
        {
            doc.Play.SetActive(active && !doc.IsClosing);
            doc.Play.Tick(elapsed);
            viewport.Present(doc.Play.Frame);
        }
        void Select(int index) => doc.Play.SelectCell(index);
        void SurfaceStatus(string status)
        {
            // 详细错误保留在 tooltip，主页面给出恢复动作，不向玩家堆积栈和部署实现细节。
            _surfaceStatus.Text = viewport.Error is null ? status : "画面暂不可用，请点击“重试画面”。对局已暂停。";
            ToolTip.SetTip(_surfaceStatus, viewport.Error);
        }
        doc.Play.Changed += Refresh;
        viewport.FrameElapsed += Frame;
        viewport.CellSelected += Select;
        viewport.StatusChanged += SurfaceStatus;
        _binding = new BindingLease(() =>
        {
            doc.Play.Changed -= Refresh;
            viewport.FrameElapsed -= Frame;
            viewport.CellSelected -= Select;
            viewport.StatusChanged -= SurfaceStatus;
            doc.Play.SetActive(false);
            viewport.Dispose();
        });
        doc.AttachSurface(_binding);
        viewport.Present(doc.Play.Frame);
        _stage.Child = viewport;
        Refresh();
    }

    private void Refresh()
    {
        if (_document is not { } doc) return;
        var play = doc.Play;
        var snapshot = play.Snapshot;
        _refreshing = true;
        try
        {
            _round.Text = $"第 {snapshot.Round} / 30 轮  ·  {RichTownPresentation.Player(snapshot.CurrentPlayerId)}";
            _status.Text = RichTownPresentation.Status(play);
            _die.Text = play.HasRolled ? $"骰子  {play.LastDie} 点" : "从起点出发，建设你的小镇";
            for (var id = 0; id < 3; id++)
            {
                var player = snapshot.Players[id];
                var count = snapshot.Properties.Count(property => property.OwnerId == id);
                _players[id].Text = $"{(id == snapshot.CurrentPlayerId && snapshot.Phase != RichTownPhase.Finished ? "▶ " : "")}{RichTownPresentation.Player(id)}{(player.IsEliminated ? " · 已出局" : "")}\n现金 {player.Cash:N0}  ·  地产 {count} 块\n位置 {player.Position:00} · {RichTownPresentation.Cell(player.Position)}";
            }
            _cells.SelectedIndex = play.SelectedCell;
            _details.Text = RichTownPresentation.Details(snapshot, play.SelectedCell);
            _ranking.IsVisible = snapshot.Phase == RichTownPhase.Finished;
            _ranking.Text = "最终排名\n" + string.Join('\n', snapshot.GetStandings().Select(item => $"第 {item.Rank} 名  {RichTownPresentation.Player(item.PlayerId)}  {item.Score:N0}"));
            _log.Text = play.Log.Count == 0 ? "购买街区，升级房屋；每次行动后记得结束回合。" : string.Join('\n', play.Log.Reverse().Take(6));
            _pause.Content = play.IsPaused ? "继续游戏" : "暂停";
            _pause.IsEnabled = !play.IsDisposed && play.IsActive;
            _skip.IsEnabled = play.IsAnimating && !play.IsDisposed;
            _newGame.IsEnabled = doc.CanExecute(RichTownDocument.RestartCommandId);
            foreach (var (kind, button) in _actions)
            {
                var command = new RichTownCommand(kind, kind is RichTownCommandKind.Upgrade or RichTownCommandKind.Sell ? play.SelectedCell : null);
                button.Tag = new HumanAction(play.Stamp, command);
                button.IsEnabled = RichTownPresentation.Can(play, command);
            }
            _viewport?.Present(play.Frame);
        }
        finally { _refreshing = false; }
    }

    private void UpdateLayoutMode()
    {
        var narrow = Bounds.Width < 900;
        _body.ColumnDefinitions = new(narrow ? "*" : "*,300");
        _body.RowDefinitions = new(narrow ? "*,260" : "*");
        Grid.SetColumn(_sidebar, narrow ? 0 : 1);
        Grid.SetRow(_sidebar, narrow ? 1 : 0);
    }

    private sealed record HumanAction(RichTownInteractionStamp Stamp, RichTownCommand Command);
    private sealed class BindingLease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() { var action = _release; _release = null; action?.Invoke(); }
    }
}
