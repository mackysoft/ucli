using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;
using MackySoft.Ucli.UnityIntegration.Project.Resolution;
using MackySoft.Ucli.UnityIntegration.Resolution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Common;

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

        services.AddSingleton<IProjectPathInputResolver, ProjectPathInputResolver>();
        services.AddSingleton<IUnityProjectResolver>(provider => new UnityProjectResolver(
            provider.GetRequiredService<IProjectPathInputResolver>()));
        services.AddSingleton<UnityUcliPluginMarkerDiscovery>();
        services.AddSingleton<UnityUcliPluginMarkerValidator>();
        services.AddSingleton<UnityUcliPluginMarkerCacheStore>();
        services.AddSingleton<UnityUcliPluginMarkerCacheCoordinator>();
        services.AddSingleton<IUnityUcliPluginLocator, UnityUcliPluginLocator>();
        services.AddSingleton<IUnityPluginVerifier, UnityPluginVerifier>();
        services.AddSingleton<IUnityVersionResolver, UnityVersionResolver>();
        services.AddSingleton<IUnityEditorSearchRootProvider, DefaultUnityEditorSearchRootProvider>();
        services.AddSingleton<IUnityEditorPathResolver, UnityEditorPathResolver>();
        services.AddSingleton<IIndexCatalogReader, FileIndexCatalogReader>();
        services.AddSingleton<IMutationReadPostconditionStore, MutationReadPostconditionStore>();
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
        services.AddSingleton<IPersistedOpsCatalogReader, PersistedOpsCatalogReader>();
        services.AddSingleton<IPersistedOpsCatalogPersistenceArtifactsReader, PersistedOpsCatalogPersistenceArtifactsReader>();
        services.AddSingleton<IIpcEndpointResolver, IpcEndpointResolver>();
        services.AddSingleton<IIpcTransportClient, IpcTransportClient>();
        services.AddSingleton<IUnityIpcTransportClient, UnityIpcTransportClient>();
        services.AddSingleton<IUnityIpcClient, UnityDaemonIpcClient>();
        services.AddSingleton<IUnityIpcClient, UnityOneshotIpcClient>();
        services.AddSingleton<IUnityRequestExecutor, UnityIpcRequestExecutor>();
        return services;
    }
}
