using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Provides operation descriptor values to the operation catalog. </summary>
internal interface IOperationCatalogProvider
{
    /// <summary> Asynchronously gets operation descriptor values used for catalog construction. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the operation descriptor collection. </returns>
    ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperationsAsync (CancellationToken cancellationToken = default);

    /// <summary> Asynchronously gets operation descriptor values for the specified resolved Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="config"> The loaded configuration used to execute catalog discovery. </param>
    /// <param name="failFast"> Whether live catalog discovery should fail immediately instead of waiting for Unity readiness. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the operation descriptor collection. </returns>
    ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperationsAsync (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        UnityExecutionMode mode = UnityExecutionMode.Auto,
        TimeSpan? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}
