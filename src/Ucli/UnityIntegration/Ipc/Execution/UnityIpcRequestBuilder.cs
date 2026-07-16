using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Converts application Unity request payloads into IPC method dispatch requests. </summary>
internal sealed class UnityIpcRequestBuilder
{
    /// <summary> Converts one application request into the IPC method and serialized payload. </summary>
    /// <param name="request"> The application request payload. </param>
    /// <returns> The IPC dispatch request. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    public UnityIpcDispatchRequest Build (UnityRequestPayload request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request switch
        {
            UnityRequestPayload.OpsRead opsRead => new UnityIpcDispatchRequest(
                UnityIpcMethod.OpsRead,
                IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(
                    opsRead.FailFast,
                    opsRead.RequireReadinessGate,
                    opsRead.IncludeEditLoweringOnly)),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.IndexAssetsRead indexAssetsRead => new UnityIpcDispatchRequest(
                UnityIpcMethod.IndexAssetsRead,
                IpcPayloadCodec.SerializeToElement(new IpcIndexAssetsReadRequest(indexAssetsRead.FailFast)),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.IndexSceneTreeLiteRead indexSceneTreeLiteRead => new UnityIpcDispatchRequest(
                UnityIpcMethod.IndexSceneTreeLiteRead,
                IpcPayloadCodec.SerializeToElement(new IpcIndexSceneTreeLiteReadRequest(
                    indexSceneTreeLiteRead.ScenePath,
                    indexSceneTreeLiteRead.FailFast,
                    indexSceneTreeLiteRead.LoadedSceneOnly)),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.Ping ping => new UnityIpcDispatchRequest(
                UnityIpcMethod.Ping,
                IpcPayloadCodec.SerializeToElement(new IpcPingRequest(ping.ClientVersion, ping.FailFast)),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.Compile compile => new UnityIpcDispatchRequest(
                UnityIpcMethod.Compile,
                IpcPayloadCodec.SerializeToElement(new IpcCompileRequest(compile.RunId)),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.BuildRun buildRun => new UnityIpcDispatchRequest(
                UnityIpcMethod.BuildRun,
                IpcPayloadCodec.SerializeToElement(buildRun.Request),
                new UnityBatchmodeLaunchOptions(
                    buildRun.Request.InputKind == BuildProfileInputsKind.UnityBuildProfile
                        ? buildRun.Request.UnityBuildProfile?.Path
                        : null)),
            UnityRequestPayload.TestRun testRun => new UnityIpcDispatchRequest(
                UnityIpcMethod.TestRun,
                IpcPayloadCodec.SerializeToElement(new IpcTestRunRequest(
                    TestPlatform: TestRunPlatformCodec.ToValue(testRun.TestPlatform),
                    TestFilter: testRun.TestFilter,
                    TestCategories: testRun.TestCategories,
                    AssemblyNames: testRun.AssemblyNames,
                    FailFast: testRun.FailFast,
                    RunId: testRun.RunId)),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.PlayStatus => new UnityIpcDispatchRequest(
                UnityIpcMethod.PlayStatus,
                IpcPayloadCodec.SerializeToElement(new IpcPlayStatusRequest()),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.ScreenshotCapture screenshotCapture => new UnityIpcDispatchRequest(
                UnityIpcMethod.ScreenshotCapture,
                IpcPayloadCodec.SerializeToElement(screenshotCapture.Request),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.PlayEnter => new UnityIpcDispatchRequest(
                UnityIpcMethod.PlayEnter,
                IpcPayloadCodec.SerializeToElement(new IpcPlayEnterRequest()),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.PlayExit => new UnityIpcDispatchRequest(
                UnityIpcMethod.PlayExit,
                IpcPayloadCodec.SerializeToElement(new IpcPlayExitRequest()),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.ExecuteJson executeJson => new UnityIpcDispatchRequest(
                UnityIpcMethod.Execute,
                CreateExecutePayload(
                    executeJson.Command,
                    executeJson.ExecuteArguments,
                    executeJson.FailFast,
                    executeJson.AllowDangerous,
                    executeJson.PlanToken,
                    executeJson.AllowPlayMode),
                UnityBatchmodeLaunchOptions.Default),
            UnityRequestPayload.ExecuteOperation executeOperation => new UnityIpcDispatchRequest(
                UnityIpcMethod.Execute,
                CreateExecutePayload(
                    executeOperation.Command,
                    CreateSingleOperationArguments(
                        executeOperation.OperationId,
                        executeOperation.OperationName,
                        executeOperation.Args),
                    executeOperation.FailFast,
                    executeOperation.AllowDangerous,
                    executeOperation.PlanToken,
                    allowPlayMode: false),
                UnityBatchmodeLaunchOptions.Default),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, "Unsupported Unity request payload."),
        };
    }

    private static JsonElement CreateSingleOperationArguments (
        IpcExecuteStepId operationId,
        string operationName,
        JsonElement args)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return JsonSerializer.SerializeToElement(new
        {
            protocolVersion = IpcProtocol.CurrentVersion,
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
        ArgumentNullException.ThrowIfNull(command);

        return IpcPayloadCodec.SerializeToElement(new IpcExecuteRequest(command.Name, executeArguments)
        {
            AllowPlayMode = allowPlayMode,
            AllowDangerous = allowDangerous,
            FailFast = failFast,
            PlanToken = planToken,
        });
    }
}
