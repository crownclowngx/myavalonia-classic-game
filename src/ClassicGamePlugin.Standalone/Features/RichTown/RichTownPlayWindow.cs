using Avalonia.Controls;
using ClassicGamePlugin.Features.RichTown;
using ClassicGamePlugin.Features.RichTown.Views;
using ClassicGamePlugin.Features.RichTown.Domain;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Standalone.Features.RichTown;

/// <summary>G3 本地可玩入口，复用插件同一 Document/View；不在 Standalone 复制玩法规则。</summary>
internal sealed class RichTownPlayWindow : Window
{
    internal RichTownDocument Document { get; }
    internal RichTownDocumentView View { get; }
    public RichTownPlayWindow(string? checkReport = null) : this(
        checkReport is null ? new RichTownDocument() : new RichTownDocument(RichTownSnapshot.Create(19)),
        new NewDocumentActivation(string.Empty), checkReport) { }

    // 检查入口只替换 SDK 激活数据，始终复用正式 Document/View 和同一静态租约。
    internal static RichTownPlayWindow ForIntegration(DocumentContent? content) => new(
        new RichTownDocument(RichTownSnapshot.Create(19)),
        content is null ? new NewDocumentActivation("本机集成检查") : new RestoreDocumentActivation("恢复检查", content), null);

    private RichTownPlayWindow(RichTownDocument document, DocumentActivation activation, string? checkReport)
    {
        Title = "富翁小镇 · 3D 地产游戏";
        Width = 1200;
        Height = 900;
        MinWidth = 700;
        MinHeight = 760;
        document.InitializeAsync(activation, default).GetAwaiter().GetResult();
        Document = document;
        var view = View = new RichTownDocumentView { DataContext = document };
        Content = view;
        Closed += (_, _) => document.Dispose();
        if (checkReport is not null)
        {
            var check = new RichTownPlayCheck(this, document, view, checkReport);
            Opened += (_, _) => check.Start();
            Closed += (_, _) => check.Dispose();
        }
    }
}
