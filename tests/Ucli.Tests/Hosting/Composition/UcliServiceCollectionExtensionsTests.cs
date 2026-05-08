using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Hosting.Composition.Common;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests.Hosting.Composition;

public sealed class UcliServiceCollectionExtensionsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AddUcliServices_ResolvesReadIndexPolicyAndAdapterGraph ()
    {
        var services = new ServiceCollection();
        services.AddUcliServices();

        using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        Assert.NotNull(serviceProvider.GetRequiredService<IReadIndexArtifactReader>());
        Assert.NotNull(serviceProvider.GetRequiredService<IReadIndexArtifactWriter>());
        Assert.NotNull(serviceProvider.GetRequiredService<IReadIndexFreshnessEvaluator>());
        Assert.NotNull(serviceProvider.GetRequiredService<IOpsCatalogSourceRefreshService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IAssetLookupSourceRefreshService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISceneTreeLiteSourceRefreshService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISceneTreeLiteDirtySourceProbeService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IPersistedOpsCatalogPersistenceArtifactsReader>());
    }
}
