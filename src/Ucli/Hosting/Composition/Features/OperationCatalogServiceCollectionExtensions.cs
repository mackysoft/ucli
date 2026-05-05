using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Persistence;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

/// <summary> Registers services for the <c>ops</c> feature boundary. </summary>
internal static class OperationCatalogServiceCollectionExtensions
{
    /// <summary> Registers operation catalog feature services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliOperationCatalogFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IOpsCatalogReader, OpsCatalogReader>();
        services.AddSingleton<IOpsCatalogInputFingerprintCalculator, InfrastructureOpsCatalogInputFingerprintCalculator>();
        services.AddSingleton<IOpsCatalogStore, FileOpsCatalogStore>();
        services.AddSingleton<IOperationCatalogWarmup, OperationCatalogWarmupService>();
        return services;
    }
}
