using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Everything.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEverythingClient(this IServiceCollection services)
    {
        services.TryAddSingleton<IEverythingClient, EverythingClient>();
        return services;
    }

    public static IServiceCollection AddEverythingClient(this IServiceCollection services, Action<EverythingClientOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.TryAddSingleton<IEverythingClient, EverythingClient>();
        return services;
    }
}