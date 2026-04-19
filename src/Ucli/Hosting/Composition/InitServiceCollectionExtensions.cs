using MackySoft.Ucli.Features.Init;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition;

/// <summary> Registers feature services for the <c>init</c> command. </summary>
internal static class InitServiceCollectionExtensions
{
    /// <summary> Registers init feature services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliInitFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IInitService, InitService>();
        return services;
    }
}