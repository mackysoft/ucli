using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Shared.Execution.UnityRequest;

/// <summary> Executes one Unity IPC request through the resolved daemon or oneshot host. </summary>
internal interface IUnityRequestExecutor
{
    /// <summary> Executes one Unity IPC request through the configured execution mode policy. </summary>
    /// <param name="command"> The command that owns the request execution. </param>
    /// <param name="mode"> The normalized requested Unity execution mode. </param>
    /// <param name="timeout"> The resolved timeout budget for this IPC request. </param>
    /// <param name="config"> The loaded uCLI configuration. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The IPC method payload. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The IPC execution result. </returns>
    ValueTask<UnityRequestExecutionResult> Execute (
        UcliCommand command,
        MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        string method,
        JsonElement payload,
        CancellationToken cancellationToken = default);
}