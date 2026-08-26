using Microsoft.Extensions.DependencyInjection;

namespace ClassicGamePlugin.Plugin;

public static class ClassicGamePluginServices
{
    /// <summary>登记插件自己的业务服务；Standalone 可以复用同一个组合入口。</summary>
    public static IServiceCollection AddClassicGamePluginServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
