using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

/// <summary> Registers feature services for the <c>status</c> command. </summary>
internal static class StatusServiceCollectionExtensions
{
    /// <summary> Registers status feature services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliStatusFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
