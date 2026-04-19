using MackySoft.Ucli.Hosting.Cli.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition;

/// <summary> Registers hosting-layer services used by CLI entrypoints. </summary>
internal static class HostingServiceCollectionExtensions
{
    /// <summary> Registers hosting-layer services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliHostingServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRequestInputReader, RequestInputReader>();
        return services;
    }
}