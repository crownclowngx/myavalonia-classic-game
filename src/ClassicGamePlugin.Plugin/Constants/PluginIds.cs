using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Constants;

public static class PluginIds
{
    public static readonly PluginId Plugin = new("myavalonia.plugin.classic.game");

    public static readonly DocumentTypeId MainDocument =
        new("myavalonia.plugin.classic.game.document.main");
}
