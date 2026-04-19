using MackySoft.Ucli.Features.OperationCatalog;
using MackySoft.Ucli.Features.OperationCatalog.Access;
using MackySoft.Ucli.Features.OperationCatalog.Mapping;
using MackySoft.Ucli.Features.OperationCatalog.Preflight;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition;

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
        services.AddSingleton<IOpsCatalogStore, FileOpsCatalogStore>();
        services.AddSingleton<IOpsPreflightService, OpsPreflightService>();
        services.AddSingleton<IOpsCatalogAccessService, OpsCatalogAccessService>();
        services.AddSingleton<OpsReadIndexInfoMapper>();
        services.AddSingleton<IOpsListResultMapper, OpsListResultMapper>();
        services.AddSingleton<IOpsDescribeResultMapper, OpsDescribeResultMapper>();
        services.AddSingleton<IOpsService, OpsService>();
        return services;
    }
}