using MackySoft.Ucli.Application;
using MackySoft.Ucli.Hosting.Composition.Features;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Common;

/// <summary> Provides DI registration helpers for uCLI application composition roots. </summary>
internal static class UcliServiceCollectionExtensions
{
    /// <summary> Registers all services required by the uCLI hosting process. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUcliHostingServices();
        services.AddUcliApplicationServices();
        services.AddUcliSharedServices();
        services.AddUcliUnityIntegrationServices();
        services.AddUcliAssuranceFeatureServices();
        services.AddUcliScreenshotFeatureServices();
        services.AddUcliInitFeatureServices();
        services.AddUcliOperationCatalogFeatureServices();
        services.AddUcliDaemonFeatureServices();
        services.AddUcliTestingFeatureServices();
        services.AddUcliSkillsFeatureServices();
        return services;
    }
}
