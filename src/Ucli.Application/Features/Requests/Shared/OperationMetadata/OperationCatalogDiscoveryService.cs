using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Executes operation-catalog discovery through the shared ops reader and maps failures into structured catalog-load exceptions. </summary>
internal sealed class OperationCatalogDiscoveryService : IOperationCatalogDiscoveryService
{
    private readonly IOpsCatalogReader opsCatalogReader;

    /// <summary> Initializes a new instance of the <see cref="OperationCatalogDiscoveryService" /> class. </summary>
    /// <param name="opsCatalogReader"> The ops catalog reader dependency. </param>
    public OperationCatalogDiscoveryService (IOpsCatalogReader opsCatalogReader)
    {
        this.opsCatalogReader = opsCatalogReader ?? throw new ArgumentNullException(nameof(opsCatalogReader));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> DiscoverAsync (
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

        var effectiveTimeout = timeout;
        if (!effectiveTimeout.HasValue)
        {
            var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
                optionValue: null,
                UcliCommandIds.Ops,
                config);
            if (!timeoutResolutionResult.IsSuccess)
            {
                throw OperationCatalogLoadException.Create(
                    ApplicationFailure.FromExecutionError(timeoutResolutionResult.Error!),
                    "Operation catalog timeout could not be resolved.");
            }

            effectiveTimeout = timeoutResolutionResult.Timeout;
        }

        var resolvedTimeout = effectiveTimeout
            ?? throw new InvalidOperationException("Operation catalog timeout must be resolved before discovery begins.");

        var catalogResult = await opsCatalogReader.ReadAsync(
                unityProject,
                config,
                mode,
                resolvedTimeout,
                failFast,
                requireReadinessGate: false,
                includeEditLoweringOnly: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (catalogResult is OpsCatalogFetchResult.Failed failedCatalogRead)
        {
            throw OperationCatalogLoadException.Create(
                failedCatalogRead.Error,
                "Operation catalog discovery failed.");
        }

        var successfulCatalogRead = catalogResult as OpsCatalogFetchResult.Succeeded
            ?? throw new InvalidOperationException($"Unsupported ops-catalog fetch result '{catalogResult.GetType().Name}'.");
        return OperationDescriptorMapper.Map(successfulCatalogRead.Snapshot.Operations, cancellationToken);
    }
}
