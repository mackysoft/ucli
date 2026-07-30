using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Builds the operation catalog from discovered operation metadata. </summary>
internal sealed class OperationCatalogProvider : IOperationCatalogProvider
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IOperationCatalogDiscoveryService operationCatalogDiscoveryService;

    /// <summary> Initializes a new instance of the <see cref="OperationCatalogProvider" /> class. </summary>
    /// <param name="projectContextResolver"> The shared context resolver dependency. </param>
    /// <param name="operationCatalogDiscoveryService"> The operation-catalog discovery dependency. </param>
    public OperationCatalogProvider (
        IProjectContextResolver projectContextResolver,
        IOperationCatalogDiscoveryService operationCatalogDiscoveryService)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.operationCatalogDiscoveryService = operationCatalogDiscoveryService ?? throw new ArgumentNullException(nameof(operationCatalogDiscoveryService));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperationsAsync (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await projectContextResolver.ResolveAsync(
                projectPath: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            throw OperationCatalogLoadException.Create(
                ApplicationFailure.FromExecutionError(contextResult.Error!),
                "Operation catalog context could not be resolved.");
        }

        return await operationCatalogDiscoveryService.DiscoverAsync(
                contextResult.Context!.UnityProject,
                contextResult.Context.Config,
                mode: UnityExecutionMode.Auto,
                timeout: null,
                failFast: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperationsAsync (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan? timeout,
        bool failFast,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(config);

        return await operationCatalogDiscoveryService.DiscoverAsync(
                unityProject,
                config,
                mode,
                timeout,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
