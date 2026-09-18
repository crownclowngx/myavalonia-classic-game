using System.Security.Cryptography;
using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Persistence;
using ClassicGamePlugin.Features.RichTown.Presentation;
using Microsoft.Extensions.DependencyInjection;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.RichTown;

/// <summary>
/// 小镇独立的 SDK 适配器：拥有对局、激活租约、保存修订和最终释放，不承担规则、文件事务或 GPU 绘制。
/// Initialize/Restart/Dispose 在 Host 的 UI 生命周期调用；保存只读取锁保护的不可变快照，可与 UI 更新交错。
/// 同一实例只初始化一次，恢复先完整解码再提交，临时移出视觉树不归还对局租约。
/// </summary>
public sealed class RichTownDocument : IPersistablePluginDocument, IWorkbenchDocumentCommandTarget, IDisposable
{
    public static DocumentTypeId TypeId { get; } = new("myavalonia.plugin.classic.game.document.rich-town");
    public static CommandId RestartCommandId { get; } = new("myavalonia.plugin.classic.game.command.rich-town.restart");
    private readonly object _lifecycle = new();
    private readonly RichTownSessionLease _lease;
    private readonly RichTownSaveTracker _save;
    private readonly CancellationToken _closing;
    private readonly CancellationTokenRegistration _lifetimeRegistration;
    private DocumentPresentationState _presentation = new("富翁小镇 3D（开发版）");
    private IDisposable? _sessionToken;
    private IDisposable? _surface;
    private volatile bool _disposed;
    private volatile bool _initialized;
    internal RichTownPlayController Play { get; }
    internal bool IsInitialized => _initialized;
    internal bool IsClosing => _disposed || _closing.IsCancellationRequested;

    public RichTownDocument() : this(RichTownSnapshot.Create(NewSeed()), RichTownSessionLease.Shared) { }
    /// <summary>真实 Host 注入公开关闭信号；无 Host 的开发窗口使用无参构造并由窗口显式 Dispose。</summary>
    [ActivatorUtilitiesConstructor]
    public RichTownDocument(IDocumentLifetime lifetime) : this(RichTownSnapshot.Create(NewSeed()), RichTownSessionLease.Shared, lifetime) { }
    internal RichTownDocument(RichTownSnapshot snapshot, RichTownSessionLease? lease = null, IDocumentLifetime? lifetime = null)
    {
        _lease = lease ?? RichTownSessionLease.Shared;
        Play = new(snapshot);
        _save = new(new(snapshot));
        Play.Changed += OnPlayChanged;
        _closing = lifetime?.ClosingToken ?? default;
        _lifetimeRegistration = _closing.Register(OnClosing);
    }

    private static ulong NewSeed() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
    public DocumentPresentationState Presentation => _presentation;
    public bool IsDirty => _save.IsDirty;
    public event EventHandler? PresentationChanged;
    public event EventHandler? IsDirtyChanged;
    public event EventHandler<WorkbenchCommandStateChangedEventArgs>? CommandStateChanged;

    public ValueTask InitializeAsync(DocumentActivation activation, CancellationToken cancellationToken)
    {
        lock (_lifecycle)
        {
            CheckOpen(cancellationToken);
            ArgumentNullException.ThrowIfNull(activation);
            if (_initialized) throw new InvalidOperationException("小镇 Document 已初始化，不能重复激活或覆盖当前对局。");
            if (activation is NewDocumentActivation { CreationIntentId: not null })
                throw new ArgumentException("小镇没有声明细分创建入口。", nameof(activation));
            var restored = activation is RestoreDocumentActivation restore ? RichTownContentCodec.Decode(restore.RestoredContent) : null;
            CheckOpen(cancellationToken);
            var token = _lease.Acquire();
            try
            {
                CheckOpen(cancellationToken);
                if (restored is not null) Play.Restore(restored.Snapshot, restored.LastDie, restored.HasRolled);
                _save.Initialize(CurrentSaveState(), Play.Generation);
                _initialized = true;
                _sessionToken = token;
            }
            catch { token.Dispose(); throw; }
            if (!string.IsNullOrWhiteSpace(activation.Title) && activation.Title != _presentation.Title)
            {
                _presentation = new(activation.Title);
                PresentationChanged?.Invoke(this, EventArgs.Empty);
            }
            NotifyCommandState();
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask<DocumentSaveSnapshot> CaptureSaveSnapshotAsync(CancellationToken cancellationToken)
    {
        (DocumentRevision Revision, RichTownSaveState State) captured;
        lock (_lifecycle)
        {
            CheckInitialized(cancellationToken);
            captured = _save.Capture();
        }
        // 编码不持生命周期锁。此后即便 UI 提交新动作，返回的修订与内容仍来自同一份不可变观察。
        var content = RichTownContentCodec.Encode(captured.State);
        CheckOpen(cancellationToken);
        return ValueTask.FromResult(new DocumentSaveSnapshot(captured.Revision, content));
    }

    public void AcceptChanges(DocumentRevision savedRevision)
    {
        lock (_lifecycle)
        {
            if (IsClosing || !_initialized) return;
            if (_save.Accept(savedRevision)) IsDirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private RichTownSaveState CurrentSaveState() => new(Play.Snapshot, Play.LastDie, Play.HasRolled);
    private void OnPlayChanged()
    {
        if (!IsClosing && _initialized && _save.Observe(CurrentSaveState(), Play.Generation))
            IsDirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CanExecute(CommandId commandId)
    {
        ArgumentNullException.ThrowIfNull(commandId);
        return commandId == RestartCommandId && _initialized && !IsClosing;
    }
    public ValueTask ExecuteAsync(CommandId commandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandId);
        cancellationToken.ThrowIfCancellationRequested();
        if (commandId != RestartCommandId) throw new ArgumentOutOfRangeException(nameof(commandId), "当前小镇不拥有该命令。");
        if (!CanExecute(commandId)) throw new InvalidOperationException("当前小镇不能重新开始。");
        CheckOpen(cancellationToken);
        Play.Restart(RichTownSnapshot.Create(NewSeed()));
        return ValueTask.CompletedTask;
    }
    internal void NewGame() { if (CanExecute(RestartCommandId)) ExecuteAsync(RestartCommandId, default).GetAwaiter().GetResult(); }

    private void CheckOpen(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        _closing.ThrowIfCancellationRequested();
    }
    private void CheckInitialized(CancellationToken cancellationToken)
    {
        CheckOpen(cancellationToken);
        if (!_initialized) throw new InvalidOperationException("小镇尚未完成初始化。");
    }
    // 关闭通知可能来自工作线程，只通知 SDK 的命令状态；下一帧会检查 IsClosing，绝不在此访问控件或 GPU。
    private void OnClosing() { if (!_disposed) NotifyCommandState(); }
    private void NotifyCommandState() => CommandStateChanged?.Invoke(this, new(RestartCommandId));

    /// <summary>替换 View 释放旧表面但保留会话；关闭后的绑定立即释放，不能重启 GPU。</summary>
    internal void AttachSurface(IDisposable surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (ReferenceEquals(surface, _surface)) return;
        if (IsClosing) { surface.Dispose(); return; }
        _surface?.Dispose();
        _surface = surface;
    }

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed) return;
            _disposed = true;
            // 只解除订阅，不等待可能正在通知 Host 的回调，避免 UI 线程互等。没有后台工作需要另建取消源。
            _lifetimeRegistration.Unregister();
            Play.Changed -= OnPlayChanged;
            try { Play.Dispose(); }
            finally
            {
                try { _surface?.Dispose(); }
                finally
                {
                    _surface = null;
                    _sessionToken?.Dispose();
                    _sessionToken = null;
                    NotifyCommandState();
                    CommandStateChanged = null;
                    PresentationChanged = null;
                    IsDirtyChanged = null;
                }
            }
        }
    }
}
