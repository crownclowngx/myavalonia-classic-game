using MyAvaloniaManagement.PluginSdk.UI;
using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Minesweeper;
using ClassicGamePlugin.Features.Minesweeper.Views;

namespace ClassicGamePlugin.Plugin;

public sealed class ClassicGamePluginModule : IPluginModule
{
    public void Configure(IPluginRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        registration.Services.AddClassicGamePluginServices();
        registration.AddDocument<MinesweeperDocument, MinesweeperDocumentView>(
            new DocumentDescriptor(
                PluginIds.MinesweeperDocument,
                "扫雷",
                "经典扫雷游戏：翻开安全格、标记地雷并完成整张棋盘",
                "经典游戏"));
    }
}
