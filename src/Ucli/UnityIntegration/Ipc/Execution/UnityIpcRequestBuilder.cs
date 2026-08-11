using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
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
    public UnityIpcDispatchRequest Build (
        UnityRequestPayload request,
        ILifecycleExecutionStartObserver? lifecycleStartObserver = null)
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
            UnityRequestPayload.Refresh refresh => UnityIpcDispatchRequest.LifecycleExecution(
                UnityIpcMethod.Refresh,
                refresh.Registration,
                refresh.RequiredStart,
                static start => IpcPayloadCodec.SerializeToElement(new IpcRefreshRequest(start)),
                refresh.StartAdmissionPolicy,
                lifecycleStartObserver),
            UnityRequestPayload.Compile compile => UnityIpcDispatchRequest.LifecycleExecution(
                UnityIpcMethod.Compile,
                compile.Registration,
                compile.RequiredStart,
                static start => IpcPayloadCodec.SerializeToElement(new IpcCompileRequest(start)),
                lifecycleStartObserver: lifecycleStartObserver),
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
            UnityRequestPayload.PlayEnter playEnter => UnityIpcDispatchRequest.LifecycleExecution(
                UnityIpcMethod.PlayEnter,
                playEnter.Registration,
                playEnter.RequiredStart,
                static start => IpcPayloadCodec.SerializeToElement(new IpcPlayEnterRequest(start)),
                lifecycleStartObserver: lifecycleStartObserver),
            UnityRequestPayload.PlayExit playExit => UnityIpcDispatchRequest.LifecycleExecution(
                UnityIpcMethod.PlayExit,
                playExit.Registration,
                playExit.RequiredStart,
                static start => IpcPayloadCodec.SerializeToElement(new IpcPlayExitRequest(start)),
                lifecycleStartObserver: lifecycleStartObserver),
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
        string operationName,
        JsonElement args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return JsonSerializer.SerializeToElement(new
        {
            protocolVersion = IpcProtocol.CurrentVersion,
            steps = new[]
            {
                new
                {
                    kind = "op",
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
