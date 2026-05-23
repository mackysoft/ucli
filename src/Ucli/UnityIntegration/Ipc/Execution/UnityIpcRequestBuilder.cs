using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Converts application Unity request payloads into IPC method dispatch requests. </summary>
internal sealed class UnityIpcRequestBuilder
{
    private static readonly IReadOnlyList<string> CompileAllowedStartupLifecycleStates =
    [
        IpcEditorLifecycleStateCodec.CompileFailed,
        IpcEditorLifecycleStateCodec.SafeMode,
    ];

    /// <summary> Converts one application request into the IPC method and serialized payload. </summary>
    /// <param name="request"> The application request payload. </param>
    /// <returns> The IPC dispatch request. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    public UnityIpcDispatchRequest Build (UnityRequestPayload request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request switch
        {
            UnityRequestPayload.Raw raw => new UnityIpcDispatchRequest(raw.Method, raw.Payload),
            UnityRequestPayload.Ping ping => new UnityIpcDispatchRequest(
                IpcMethodNames.Ping,
                IpcPayloadCodec.SerializeToElement(new IpcPingRequest(ping.ClientVersion, ping.FailFast))),
            UnityRequestPayload.Compile compile => new UnityIpcDispatchRequest(
                IpcMethodNames.Compile,
                IpcPayloadCodec.SerializeToElement(new IpcCompileRequest(compile.RunId)),
                CompileAllowedStartupLifecycleStates,
                isRecoverable: true),
            UnityRequestPayload.PlayStatus => new UnityIpcDispatchRequest(
                IpcMethodNames.PlayStatus,
                IpcPayloadCodec.SerializeToElement(new IpcPlayStatusRequest())),
            UnityRequestPayload.PlayEnter playEnter => new UnityIpcDispatchRequest(
                IpcMethodNames.PlayEnter,
                IpcPayloadCodec.SerializeToElement(new IpcPlayEnterRequest
                {
                    TimeoutMilliseconds = playEnter.TimeoutMilliseconds,
                }),
                isRecoverable: true),
            UnityRequestPayload.PlayExit playExit => new UnityIpcDispatchRequest(
                IpcMethodNames.PlayExit,
                IpcPayloadCodec.SerializeToElement(new IpcPlayExitRequest
                {
                    TimeoutMilliseconds = playExit.TimeoutMilliseconds,
                }),
                isRecoverable: true),
            UnityRequestPayload.ExecuteJson executeJson => new UnityIpcDispatchRequest(
                IpcMethodNames.Execute,
                CreateExecutePayload(
                    executeJson.Command,
                    executeJson.ExecuteArguments,
                    executeJson.FailFast,
                    executeJson.AllowDangerous,
                    executeJson.PlanToken,
                    executeJson.AllowPlayMode)),
            UnityRequestPayload.ExecuteOperation executeOperation => new UnityIpcDispatchRequest(
                IpcMethodNames.Execute,
                CreateExecutePayload(
                    executeOperation.Command,
                    CreateSingleOperationArguments(
                        executeOperation.RequestId,
                        executeOperation.OperationId,
                        executeOperation.OperationName,
                    executeOperation.Args),
                    executeOperation.FailFast,
                    executeOperation.AllowDangerous,
                    executeOperation.PlanToken,
                    allowPlayMode: false)),
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
        bool allowDangerous,
        string? planToken,
        bool allowPlayMode)
    {
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        return IpcPayloadCodec.SerializeToElement(new IpcExecuteRequest(command, executeArguments)
        {
            AllowPlayMode = allowPlayMode,
            AllowDangerous = allowDangerous,
            FailFast = failFast,
            PlanToken = planToken,
        });
    }
}
