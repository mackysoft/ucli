using System.Text.Json;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Ipc;

/// <summary> Executes one Unity IPC request through the resolved daemon or oneshot host. </summary>
internal interface IUnityIpcRequestExecutor
{
    /// <summary> Executes one Unity IPC request through the configured execution mode policy. </summary>
    /// <param name="command"> The command that owns the request execution. </param>
    /// <param name="mode"> The optional raw <c>--mode</c> option value. </param>
    /// <param name="timeout"> The optional raw <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="config"> The loaded uCLI configuration. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The IPC method payload. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The IPC execution result. </returns>
    ValueTask<UnityIpcRequestExecutionResult> Execute (
        UcliCommand command,
        string? mode,
        string? timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        string method,
        JsonElement payload,
        CancellationToken cancellationToken = default);
}