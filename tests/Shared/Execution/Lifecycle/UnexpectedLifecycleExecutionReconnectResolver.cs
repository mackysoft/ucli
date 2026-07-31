using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.TestSupport;

internal sealed class UnexpectedLifecycleExecutionReconnectResolver :
    ILifecycleExecutionReconnectResolver
{
    public ValueTask<LifecycleExecutionReconnectResolution> ResolveAsync (
        ResolvedUnityProjectContext project,
        LifecycleExecutionDefinition expectedDefinition,
        ExecutionRef executionRef,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "The workflow must not resolve an existing Lifecycle Execution registration.");
    }
}
