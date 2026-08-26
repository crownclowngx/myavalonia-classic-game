using MyAvaloniaManagement.PluginSdk.UI;
using ClassicGamePlugin.Constants;
using ClassicGamePlugin.Features.Main;

namespace ClassicGamePlugin.Plugin;

public sealed class ClassicGamePluginModule : IPluginModule
{
    public void Configure(IPluginRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        registration.Services.AddClassicGamePluginServices();
        registration.AddDocument<MainDocument, MainView>(
            new DocumentDescriptor(
                PluginIds.MainDocument,
                "示例文档",
                "由独立预览程序和真实 Host 共用的示例功能",
                "ClassicGamePlugin"));
    }
}
