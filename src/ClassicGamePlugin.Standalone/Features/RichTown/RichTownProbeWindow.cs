using Avalonia.Controls;
using ClassicGamePlugin.Features.RichTown;
using ClassicGamePlugin.Features.RichTown.Views;

namespace ClassicGamePlugin.Standalone.Features.RichTown;

/// <summary>G1 开发窗口只包装正式插件中的同一页面；不另写一份渲染或业务代码。</summary>
internal sealed class RichTownProbeWindow : Window
{
    public RichTownProbeWindow()
    {
        Title = "富翁小镇 · Stride G1 集成检查";
        Width = 1050;
        Height = 740;
        var document = new RichTownDocument();
        Content = new RichTownDocumentView { DataContext = document };
        Closed += (_, _) => document.Dispose();
    }
}
