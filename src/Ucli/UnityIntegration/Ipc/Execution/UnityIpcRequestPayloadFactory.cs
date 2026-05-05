using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Converts application Unity requests into IPC method payloads. </summary>
internal static class UnityIpcRequestPayloadFactory
{
    /// <summary> Converts one application request into the IPC method and serialized payload. </summary>
    /// <param name="request"> The application request payload. </param>
    /// <returns> The IPC method name and payload JSON. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    public static (string Method, JsonElement Payload) Create (UnityRequestPayload request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request switch
        {
            UnityRequestPayload.Raw raw => (raw.Method, raw.Payload),
            UnityRequestPayload.ExecuteJson executeJson => (
                IpcMethodNames.Execute,
                CreateExecutePayload(
                    executeJson.Command,
                    executeJson.ExecuteArguments,
                    executeJson.FailFast,
                    executeJson.PlanToken)),
            UnityRequestPayload.ExecuteOperation executeOperation => (
                IpcMethodNames.Execute,
                CreateExecutePayload(
                    executeOperation.Command,
                    CreateSingleOperationArguments(
                        executeOperation.RequestId,
                        executeOperation.OperationId,
                        executeOperation.OperationName,
                        executeOperation.Args),
                    executeOperation.FailFast,
                    executeOperation.PlanToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, "Unsupported Unity request payload."),
        };
    }

    private static JsonElement CreateSingleOperationArguments (
        string requestId,
        string operationId,
        string operationName,
        JsonElement args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return JsonSerializer.SerializeToElement(new
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
    }

    private static JsonElement CreateExecutePayload (
        UcliCommand command,
        JsonElement executeArguments,
        bool failFast,
        string? planToken)
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
