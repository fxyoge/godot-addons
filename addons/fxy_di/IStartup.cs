using Microsoft.Extensions.DependencyInjection;

namespace Fxyoge.DependencyInjection;

public interface IStartup
{
    void ConfigureServices(IServiceCollection services);
}
