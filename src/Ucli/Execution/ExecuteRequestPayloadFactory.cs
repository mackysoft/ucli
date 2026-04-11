using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Execution;

/// <summary> Creates execute-request IPC payload envelopes shared by request-driven commands. </summary>
internal static class ExecuteRequestPayloadFactory
{
    /// <summary> Wraps one execute-arguments payload with command metadata and execution options. </summary>
    /// <param name="command"> The internal execute command sent to Unity. </param>
    /// <param name="executeArguments"> The normalized execute arguments payload. </param>
    /// <param name="failFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="planToken"> The optional plan token attached to call execution. </param>
    /// <returns> The serialized IPC execute payload. </returns>
    public static JsonElement Create (
        UcliCommand command,
        JsonElement executeArguments,
        bool failFast,
        string? planToken = null)
    {
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        return IpcPayloadCodec.SerializeToElement(new IpcExecuteRequest(command, executeArguments)
        {
            FailFast = failFast,
            PlanToken = planToken,
        });
    }
}