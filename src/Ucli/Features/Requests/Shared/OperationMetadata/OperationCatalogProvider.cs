using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Hosting.Cli;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

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
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await projectContextResolver.Resolve(
                projectPath: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            throw new OperationCatalogLoadException(CreatePrefixedError(
                contextResult.Error!,
                "Operation catalog context could not be resolved."));
        }

        return await operationCatalogDiscoveryService.Discover(
                contextResult.Context!.UnityProject,
                contextResult.Context.Config,
                mode: UnityExecutionMode.Auto,
                timeout: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (
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

        return await operationCatalogDiscoveryService.Discover(
                unityProject,
                config,
                mode,
                timeout,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
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

}