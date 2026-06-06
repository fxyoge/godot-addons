using Fxyoge.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Basic.Game;

public sealed class BasicStartup : IStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMyService, MessageService>();
    }
}
