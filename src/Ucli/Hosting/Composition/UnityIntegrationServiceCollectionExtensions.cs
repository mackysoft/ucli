using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;
using MackySoft.Ucli.UnityIntegration.Resolution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition;

/// <summary> Registers Unity integration services for project resolution, indexing, and IPC execution. </summary>
internal static class UnityIntegrationServiceCollectionExtensions
{
    /// <summary> Registers Unity integration services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliUnityIntegrationServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<UnityUcliPluginMarkerDiscovery>();
        services.AddSingleton<UnityUcliPluginMarkerValidator>();
        services.AddSingleton<UnityUcliPluginMarkerCacheStore>();
        services.AddSingleton<UnityUcliPluginMarkerCacheCoordinator>();
        services.AddSingleton<IUnityUcliPluginLocator, UnityUcliPluginLocator>();
        services.AddSingleton<IProjectPathInputResolver, ProjectPathInputResolver>();
        services.AddSingleton<IUnityProjectResolver>(provider => new UnityProjectResolver(
            provider.GetRequiredService<IProjectPathInputResolver>()));
        services.AddSingleton<IUnityVersionResolver, UnityVersionResolver>();
        services.AddSingleton<IUnityEditorSearchRootProvider, DefaultUnityEditorSearchRootProvider>();
        services.AddSingleton<IUnityEditorPathResolver, UnityEditorPathResolver>();
        services.AddSingleton<IIndexCatalogReader, FileIndexCatalogReader>();
        services.AddSingleton<IIndexInputFingerprintCalculator, FileSystemIndexInputFingerprintCalculator>();
        services.AddSingleton<IIndexFreshnessEvaluator, IndexFreshnessEvaluator>();
        services.AddSingleton<IAssetLookupStore, FileAssetLookupStore>();
        services.AddSingleton<IAssetLookupSnapshotReader, AssetLookupSnapshotReader>();
        services.AddSingleton<IAssetLookupSourceRefreshService, AssetLookupSourceRefreshService>();
        services.AddSingleton<IAssetSearchLookupAccessService, AssetSearchLookupAccessService>();
        services.AddSingleton<IGuidPathLookupAccessService, GuidPathLookupAccessService>();
        services.AddSingleton<ISceneTreeLiteSourceHashCalculator, SceneTreeLiteSourceHashCalculator>();
        services.AddSingleton<ISceneTreeLiteFreshnessEvaluator, SceneTreeLiteFreshnessEvaluator>();
        services.AddSingleton<ISceneTreeLiteStore, FileSceneTreeLiteStore>();
        services.AddSingleton<ISceneTreeLiteSnapshotReader, SceneTreeLiteSnapshotReader>();
        services.AddSingleton<ISceneTreeLiteSourceRefreshService, SceneTreeLiteSourceRefreshService>();
        services.AddSingleton<ISceneTreeLiteAccessService, SceneTreeLiteAccessService>();
        services.AddSingleton<IPersistedOpsCatalogSnapshotLoader, PersistedOpsCatalogSnapshotLoader>();
        services.AddSingleton<IIpcEndpointResolver, IpcEndpointResolver>();
        services.AddSingleton<IIpcTransportClient, IpcTransportClient>();
        services.AddSingleton<IUnityIpcTransportClient, UnityIpcTransportClient>();
        services.AddSingleton<IUnityIpcClient, UnityDaemonIpcClient>();
        services.AddSingleton<IUnityIpcClient, UnityOneshotIpcClient>();
        services.AddSingleton<IUnityIpcRequestExecutor, UnityIpcRequestExecutor>();
        return services;
    }
}