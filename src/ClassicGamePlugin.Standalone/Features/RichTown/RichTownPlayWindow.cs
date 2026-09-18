using Avalonia.Controls;
using ClassicGamePlugin.Features.RichTown;
using ClassicGamePlugin.Features.RichTown.Views;
using ClassicGamePlugin.Features.RichTown.Domain;

namespace ClassicGamePlugin.Standalone.Features.RichTown;

/// <summary>G3 本地可玩入口，复用插件同一 Document/View；不在 Standalone 复制玩法规则。</summary>
internal sealed class RichTownPlayWindow : Window
{
    public RichTownPlayWindow(string? checkReport = null)
    {
        Title = "富翁小镇 · 3D 地产游戏";
        Width = 1200;
        Height = 900;
        MinWidth = 700;
        MinHeight = 760;
        var document = checkReport is null ? new RichTownDocument() : new RichTownDocument(RichTownSnapshot.Create(19));
        var view = new RichTownDocumentView { DataContext = document };
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
