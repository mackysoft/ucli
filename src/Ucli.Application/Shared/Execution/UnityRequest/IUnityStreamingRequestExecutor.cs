using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Executes one Unity IPC request that returns progress frames before the terminal response. </summary>
internal interface IUnityStreamingRequestExecutor
{
    /// <summary> Executes one streaming Unity IPC request through the configured execution mode policy. </summary>
    /// <param name="command"> The command that owns the request execution. </param>
    /// <param name="mode"> The normalized requested Unity execution mode. </param>
    /// <param name="timeout"> The resolved timeout budget for this IPC request. </param>
    /// <param name="config"> The loaded uCLI configuration. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="payload"> The host-executed Unity request payload. </param>
    /// <param name="onProgressFrame"> The callback invoked for each progress frame. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The IPC execution result. </returns>
    ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        Func<UnityRequestProgressFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default);
}
