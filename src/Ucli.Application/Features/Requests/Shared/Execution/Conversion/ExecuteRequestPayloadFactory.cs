using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;

/// <summary> Creates execute-request IPC payload envelopes shared by request-driven commands. </summary>
internal static class ExecuteRequestPayloadFactory
{
    /// <summary> Creates one execute-request payload containing a single operation step. </summary>
    /// <param name="command"> The internal execute command sent to Unity. </param>
    /// <param name="requestId"> The request identifier embedded in the execute arguments. </param>
    /// <param name="operationId"> The public operation identifier emitted in <c>steps[].id</c>. </param>
    /// <param name="operationName"> The public operation name emitted in <c>steps[].op</c>. </param>
    /// <param name="args"> The operation argument payload emitted in <c>steps[].args</c>. </param>
    /// <param name="failFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="planToken"> The optional plan token attached to call execution. </param>
    /// <returns> The serialized IPC execute payload. </returns>
    public static JsonElement CreateSingleOperation (
        UcliCommand command,
        string requestId,
        string operationId,
        string operationName,
        JsonElement args,
        bool failFast,
        string? planToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var executeArguments = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = IpcProtocol.CurrentVersion,
            requestId,
            steps = new[]
            {
                new
                {
                    kind = "op",
                    id = operationId,
                    op = operationName,
                    args,
                },
            },
        }, IpcJsonSerializerOptions.Default);

        return Create(command, executeArguments, failFast, planToken);
    }

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
