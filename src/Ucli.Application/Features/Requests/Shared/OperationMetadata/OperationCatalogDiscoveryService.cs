using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;

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
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> Discover (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        UnityExecutionMode mode = UnityExecutionMode.Auto,
        TimeSpan? timeout = null,
        bool failFast = false,
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
                throw new OperationCatalogLoadException(CreatePrefixedError(
                    timeoutResolutionResult.Error!,
                    "Operation catalog timeout could not be resolved."));
            }

            effectiveTimeout = timeoutResolutionResult.Timeout;
        }

        var resolvedTimeout = effectiveTimeout
            ?? throw new InvalidOperationException("Operation catalog timeout must be resolved before discovery begins.");

        var catalogResult = await opsCatalogReader.Read(
                unityProject,
                config,
                mode,
                resolvedTimeout,
                failFast,
                requireReadinessGate: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            throw new OperationCatalogLoadException(
                CreateErrorFromCode(
                    catalogResult.ErrorCode!.Value,
                    $"Operation catalog discovery failed. {catalogResult.Message}"),
                catalogResult.ErrorCode);
        }

        return OperationDescriptorMapper.Map(catalogResult.Snapshot!.Operations, cancellationToken);
    }

    private static ExecutionError CreatePrefixedError (
        ExecutionError error,
        string messagePrefix)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(messagePrefix);

        var message = $"{messagePrefix} {error.Message}";
        return error.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => ExecutionError.InvalidArgument(message),
            ExecutionErrorKind.Timeout => ExecutionError.Timeout(message),
            _ => ExecutionError.InternalError(message),
        };
    }

    private static ExecutionError CreateErrorFromCode (
        UcliErrorCode errorCode,
        string message)
    {
        if (!errorCode.IsValid)
        {
            throw new ArgumentException("Error code must not be empty.", nameof(errorCode));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (errorCode == UcliCoreErrorCodes.InvalidArgument)
        {
            return ExecutionError.InvalidArgument(message);
        }

        if (errorCode == ExecutionErrorCodes.IpcTimeout)
        {
            return ExecutionError.Timeout(message);
        }

        return ExecutionError.InternalError(message);
    }
}
