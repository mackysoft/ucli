using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

/// <summary> Registers request feature services for refresh, resolve, validate, plan, and call commands. </summary>
internal static class RequestServiceCollectionExtensions
{
    /// <summary> Registers request feature services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliRequestFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
