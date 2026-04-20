using MackySoft.Ucli.Features.Status.UseCases.Status;
using MackySoft.Ucli.Features.Status.UseCases.Status.Observation;
using MackySoft.Ucli.Features.Status.UseCases.Status.Preflight;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition;

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

        services.AddSingleton<IStatusExecutionContextResolver, StatusExecutionContextResolver>();
        services.AddSingleton<IStatusDaemonObservationService, StatusDaemonObservationService>();
        services.AddSingleton<IStatusService, StatusService>();
        return services;
    }
}