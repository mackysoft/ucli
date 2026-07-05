using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.TestSupport;

internal sealed class UnexpectedUnityRequestExecutor : IUnityRequestExecutor, IUnityStreamingRequestExecutor
{
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
