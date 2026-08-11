using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.TestSupport;

internal sealed class UnexpectedUnityRequestExecutor : IUnityRequestExecutor, IUnityStreamingRequestExecutor
{
    public ValueTask<LifecycleExecutionHostBindingResolution> BindAsync (UnityExecutionMode mode, ResolvedUnityProjectContext project, ExecutionDeadline executionDeadline, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Unity execution host binding was not expected.");
    }

    public ValueTask<LifecycleExecutionHostBindingResolution> BindReconnectAsync (ResolvedUnityProjectContext project, LifecycleExecutionStartBinding requiredStart, ExecutionDeadline callerWaitDeadline, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Lifecycle execution reconnect binding was not expected.");
    }
    public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Unity request execution was not expected.");
    }

    public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        Func<UnityRequestProgressFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Unity streaming request execution was not expected.");
    }
}
