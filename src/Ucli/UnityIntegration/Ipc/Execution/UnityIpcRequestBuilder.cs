using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Converts application Unity request payloads into IPC method dispatch requests. </summary>
internal sealed class UnityIpcRequestBuilder
{
    private static readonly TimeSpan PlayTransitionRecoverableResponseAttemptTimeout = TimeSpan.FromMilliseconds(1000);

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
                isRecoverable: true,
                dispatchTimeoutPayloadTransformer: ApplyCompileDispatchTimeout),
            UnityRequestPayload.BuildRun buildRun => new UnityIpcDispatchRequest(
                IpcMethodNames.BuildRun,
                IpcPayloadCodec.SerializeToElement(new IpcBuildRunRequest(
                    RunId: buildRun.RunId,
                    InputKind: buildRun.InputKind,
                    BuildTarget: buildRun.BuildTarget,
                    UnityBuildTarget: buildRun.UnityBuildTarget,
                    SceneSource: buildRun.SceneSource,
                    ScenePaths: buildRun.ScenePaths,
                    Development: buildRun.Development,
                    OutputPath: buildRun.OutputPath,
                    OutputLayout: buildRun.OutputLayout,
                    BuildReportPath: buildRun.BuildReportPath,
                    BuildLogPath: buildRun.BuildLogPath,
                    AllowedEditorModes: buildRun.AllowedEditorModes,
                    ProjectMutationMode: buildRun.ProjectMutationMode,
                    RunnerKind: buildRun.RunnerKind,
                    UnityBuildProfile: buildRun.UnityBuildProfile)
                {
                    ProfilePath = buildRun.ProfilePath,
                    ProfileDigest = buildRun.ProfileDigest,
                    RunnerMethod = buildRun.RunnerMethod,
                    RunnerArguments = buildRun.RunnerArguments,
                    RunnerEnvironmentVariables = buildRun.RunnerEnvironmentVariables,
                    RunnerEnvironmentSecrets = buildRun.RunnerEnvironmentSecrets,
                    RunnerEnvironmentVariableValues = buildRun.RunnerEnvironmentVariableValues,
                    RunnerEnvironmentSecretValues = buildRun.RunnerEnvironmentSecretValues,
                }),
                dispatchTimeoutPayloadTransformer: ApplyBuildRunDispatchTimeout,
                oneshotActiveBuildProfilePath: ResolveOneshotActiveBuildProfilePath(buildRun)),
            UnityRequestPayload.TestRun testRun => new UnityIpcDispatchRequest(
                IpcMethodNames.TestRun,
                IpcPayloadCodec.SerializeToElement(new IpcTestRunRequest(
                    TestPlatform: testRun.TestPlatform,
                    TestFilter: testRun.TestFilter,
                    TestCategories: testRun.TestCategories,
                    AssemblyNames: testRun.AssemblyNames,
                    TestSettingsPath: testRun.TestSettingsPath,
                    ResultsXmlPath: testRun.ResultsXmlPath,
                    EditorLogPath: testRun.EditorLogPath,
                    FailFast: testRun.FailFast,
                    RunId: testRun.RunId)),
                dispatchTimeoutPayloadTransformer: ApplyTestRunDispatchTimeout),
            UnityRequestPayload.PlayStatus => new UnityIpcDispatchRequest(
                IpcMethodNames.PlayStatus,
                IpcPayloadCodec.SerializeToElement(new IpcPlayStatusRequest())),
            UnityRequestPayload.PlayEnter playEnter => new UnityIpcDispatchRequest(
                IpcMethodNames.PlayEnter,
                IpcPayloadCodec.SerializeToElement(new IpcPlayEnterRequest
                {
                    TimeoutMilliseconds = playEnter.TimeoutMilliseconds,
                }),
                isRecoverable: true,
                recoverableResponseAttemptTimeout: PlayTransitionRecoverableResponseAttemptTimeout),
            UnityRequestPayload.PlayExit playExit => new UnityIpcDispatchRequest(
                IpcMethodNames.PlayExit,
                IpcPayloadCodec.SerializeToElement(new IpcPlayExitRequest
                {
                    TimeoutMilliseconds = playExit.TimeoutMilliseconds,
                }),
                isRecoverable: true,
                recoverableResponseAttemptTimeout: PlayTransitionRecoverableResponseAttemptTimeout),
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

    private static JsonElement ApplyCompileDispatchTimeout (
        JsonElement payload,
        TimeSpan dispatchTimeout)
    {
        if (!IpcPayloadCodec.TryDeserialize(payload, out IpcCompileRequest compileRequest, out _))
        {
            return payload;
        }

        return IpcPayloadCodec.SerializeToElement(compileRequest with
        {
            TimeoutMilliseconds = ToTimeoutMilliseconds(dispatchTimeout),
        });
    }

    private static JsonElement ApplyTestRunDispatchTimeout (
        JsonElement payload,
        TimeSpan dispatchTimeout)
    {
        if (!IpcPayloadCodec.TryDeserialize(payload, out IpcTestRunRequest testRunRequest, out _))
        {
            return payload;
        }

        return IpcPayloadCodec.SerializeToElement(testRunRequest with
        {
            TimeoutMilliseconds = ToTimeoutMilliseconds(dispatchTimeout),
        });
    }

    private static JsonElement ApplyBuildRunDispatchTimeout (
        JsonElement payload,
        TimeSpan dispatchTimeout)
    {
        if (!IpcPayloadCodec.TryDeserialize(payload, out IpcBuildRunRequest buildRunRequest, out _))
        {
            return payload;
        }

        return IpcPayloadCodec.SerializeToElement(buildRunRequest with
        {
            TimeoutMilliseconds = ToTimeoutMilliseconds(dispatchTimeout),
        });
    }

    private static string? ResolveOneshotActiveBuildProfilePath (UnityRequestPayload.BuildRun buildRun)
    {
        if (!ContractLiteralCodec.Matches(buildRun.InputKind, BuildProfileInputsKind.UnityBuildProfile)
            || buildRun.UnityBuildProfile?.Path == null
            || !UnityAssetPathContract.IsNormalizedBuildProfileAssetPath(buildRun.UnityBuildProfile.Path))
        {
            return null;
        }

        return buildRun.UnityBuildProfile.Path;
    }

    private static int ToTimeoutMilliseconds (TimeSpan timeout)
    {
        return checked((int)Math.Ceiling(timeout.TotalMilliseconds));
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
