using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Gomoku.Domain;
using ClassicGamePlugin.Features.Gomoku.ViewModels;
using ClassicGamePlugin.Workbench;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.Gomoku;

/// <summary>
/// 五子棋与 Plugin SDK 之间的窄适配器。Document 只拥有页面 ViewModel、处理 Host 标题并释放生命周期，
/// 同时把少量跨工作台用户意图适配到当前实例；不承载棋盘规则、电脑策略或重复实现玩家用例。
/// </summary>
public sealed class GomokuDocument :
    IPluginDocument,
    IWorkbenchDocumentCommandTarget,
    IDisposable
{
    private DocumentPresentationState _presentation = new("五子棋");
    private readonly WorkbenchDocumentCommandAdapter _workbenchCommands;
    private bool _disposed;

    public GomokuDocument()
        : this(new GomokuViewModel())
    {
    }

    internal GomokuDocument(
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer,
        IReadOnlyDictionary<GomokuAiDifficulty, IGomokuMoveStrategy> computerStrategies)
        : this(new GomokuViewModel(timeProvider, enableDisplayRefreshTimer, computerStrategies))
    {
    }

    private GomokuDocument(GomokuViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _workbenchCommands = new(
            this,
            (PluginIds.RestartGomoku, ViewModel.RestartCommand),
            (PluginIds.UndoGomoku, ViewModel.UndoCommand));
    }

    public GomokuViewModel ViewModel { get; }
    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;

    /// <summary>当当前五子棋实例中的一条工作台命令状态变化时发生。</summary>
    /// <remarks>
    /// 一次事件只携带一个稳定命令身份。Host 负责把可能来自非 UI 线程的通知切换到 UI 线程，
    /// 并在活动 Document 切换和关闭时成对退订；本 Document 不持有 Host 菜单、快捷键或 Context。
    /// </remarks>
    public event EventHandler<WorkbenchCommandStateChangedEventArgs>? CommandStateChanged
    {
        add => _workbenchCommands.CommandStateChanged += value;
        remove => _workbenchCommands.CommandStateChanged -= value;
    }

    /// <summary>采用 Host 提供的非空标题初始化 Document；空白标题保留“五子棋”。</summary>
    public ValueTask InitializeAsync(DocumentActivation activation, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(activation);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(activation.Title) &&
            !string.Equals(_presentation.Title, activation.Title, StringComparison.Ordinal))
        {
            _presentation = new DocumentPresentationState(activation.Title);
            PresentationChanged?.Invoke(this, EventArgs.Empty);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>查询当前五子棋 Document 实例能否接受指定工作台命令。</summary>
    /// <param name="commandId">Host 正在查询的稳定命令身份。</param>
    /// <returns>命令属于本 Target 且当前实例状态允许执行时为 <see langword="true"/>。</returns>
    bool IWorkbenchDocumentCommandTarget.CanExecute(CommandId commandId) =>
        _workbenchCommands.CanExecute(commandId);

    /// <summary>在当前五子棋 Document 实例上执行重新开始或撤销命令。</summary>
    /// <param name="commandId">Host 已路由到当前活动实例的稳定命令身份。</param>
    /// <param name="cancellationToken">调用在开始前取消时使用的协作取消令牌。</param>
    /// <returns>同步玩家命令完成后立即结束的可等待操作。</returns>
    /// <exception cref="ArgumentOutOfRangeException">命令不属于五子棋 Target。</exception>
    /// <exception cref="InvalidOperationException">当前实例状态不允许执行该命令。</exception>
    ValueTask IWorkbenchDocumentCommandTarget.ExecuteAsync(
        CommandId commandId,
        CancellationToken cancellationToken) =>
        _workbenchCommands.ExecuteAsync(commandId, cancellationToken);

    /// <summary>级联停止 ViewModel 拥有的电脑搜索、刷新计时器和游戏计时。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // 先让工作台命令 fail closed 并成对退订，再级联释放可能仍会触发状态变化的 ViewModel。
        _workbenchCommands.Dispose();
        ViewModel.Dispose();
    }
}
