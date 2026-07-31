using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Resolves the immutable registration of an already published Lifecycle Execution reference.
/// </summary>
internal interface ILifecycleExecutionReconnectResolver
{
    /// <summary>
    /// Resolves the authoritative registration for one action-specific reconnection attempt.
    /// </summary>
    /// <param name="project"> The guarded Unity project selected by the action handler. </param>
    /// <param name="expectedDefinition"> The closed definition owned by the action handler. </param>
    /// <param name="executionRef"> The previously published execution reference. </param>
    /// <param name="cancellationToken"> The caller's cancellation token for this resolution. </param>
    /// <returns>
    /// The original immutable registration, or a typed failure that rejects the reconnection before
    /// a provider request is dispatched.
    /// </returns>
    ValueTask<LifecycleExecutionReconnectResolution> ResolveAsync (
        ResolvedUnityProjectContext project,
        LifecycleExecutionDefinition expectedDefinition,
        ExecutionRef executionRef,
        CancellationToken cancellationToken = default);
}
