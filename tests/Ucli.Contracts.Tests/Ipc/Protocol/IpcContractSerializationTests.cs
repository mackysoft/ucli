using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Testing;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcContractSerializationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    [Trait("Size", "Small")]
    public void IpcProtocol_ExposeStableLiterals ()
    {
        Assert.Equal(1, IpcProtocol.CurrentVersion);
        Assert.Equal("ok", IpcProtocol.StatusOk);
        Assert.Equal("error", IpcProtocol.StatusError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcRequest_SerializesWithCamelCaseContractFields ()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            command = "status",
        }, SerializerOptions);
        var request = new IpcRequest(
            ProtocolVersion: 1,
            RequestId: "req-1",
            SessionToken: "token",
            Method: "execute",
            Payload: payload,
            responseMode: IpcResponseMode.Single);

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(request, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("protocolVersion", 1)
            .HasString("requestId", "req-1")
            .HasString("sessionToken", "token")
            .HasString("method", "execute")
            .HasString("responseMode", ContractLiteralCodec.ToValue(IpcResponseMode.Single))
            .HasValueKind("payload", JsonValueKind.Object);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResponse_RoundTripsWithErrors ()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            result = "ok",
        }, SerializerOptions);
        var response = new IpcResponse(
            ProtocolVersion: 1,
            RequestId: "req-2",
            Status: "error",
            Payload: payload,
            Errors:
            [
                new IpcError(UcliCoreErrorCodes.CommandNotImplemented, "Not implemented", null),
            ]);

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        using var jsonDocument = JsonDocument.Parse(json);
        Assert.Equal("COMMAND_NOT_IMPLEMENTED", jsonDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());

        var roundTrip = JsonSerializer.Deserialize<IpcResponse>(json, SerializerOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(response.ProtocolVersion, roundTrip.ProtocolVersion);
        Assert.Equal(response.RequestId, roundTrip.RequestId);
        Assert.Equal(response.Status, roundTrip.Status);
        Assert.Single(roundTrip.Errors);
        Assert.Equal(UcliCoreErrorCodes.CommandNotImplemented, roundTrip.Errors[0].Code);
        Assert.Equal("Not implemented", roundTrip.Errors[0].Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_ProgressSerializesWithCamelCaseContractFields ()
    {
        var frame = new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            "req-stream",
            IpcStreamFrameKinds.Progress,
            TestRunProgressEventNames.RunStarted,
            IpcPayloadCodec.SerializeToElement(new TestRunStartedEntry(
                "run-1",
                "editmode",
                "Namespace.Tests",
                ["Assembly.Tests"],
                ["smoke"])),
            Response: null);

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(frame, SerializerOptions));

        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("protocolVersion", IpcProtocol.CurrentVersion)
            .HasString("requestId", "req-stream")
            .HasString("kind", IpcStreamFrameKinds.Progress)
            .HasString("event", TestRunProgressEventNames.RunStarted)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasValueKind("response", JsonValueKind.Null);
        JsonAssert.For(jsonDocument.RootElement.GetProperty("payload"))
            .HasString("runId", "run-1")
            .HasString("testPlatform", "editmode")
            .HasString("testFilter", "Namespace.Tests")
            .HasArrayLength("assemblyNames", 1)
            .HasArrayLength("testCategories", 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcStreamFrame_TerminalSerializesWithResponse ()
    {
        var frame = new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            "req-stream",
            IpcStreamFrameKinds.Terminal,
            Event: null,
            JsonSerializer.SerializeToElement(new { }, SerializerOptions),
            new IpcResponse(
                IpcProtocol.CurrentVersion,
                "req-stream",
                IpcProtocol.StatusOk,
                JsonSerializer.SerializeToElement(new { exitCode = 0 }, SerializerOptions),
                Array.Empty<IpcError>()));

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(frame, SerializerOptions));

        JsonAssert.For(jsonDocument.RootElement)
            .HasString("kind", IpcStreamFrameKinds.Terminal)
            .HasValueKind("event", JsonValueKind.Null)
            .HasValueKind("response", JsonValueKind.Object);
        JsonAssert.For(jsonDocument.RootElement.GetProperty("response"))
            .HasString("requestId", "req-stream")
            .HasString("status", IpcProtocol.StatusOk)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasArrayLength("errors", 0);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildPreconditionContracts_SerializeWithCamelCaseFields ()
    {
        Assert.Equal("explicit", ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit));
        Assert.Equal("editorBuildSettings", ContractLiteralCodec.ToValue(BuildProfileSceneSource.EditorBuildSettings));
        Assert.Equal("scene", ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene));

        using var dirtyState = JsonDocument.Parse(JsonSerializer.Serialize(
            new IpcBuildDirtyState(
                Checked: true,
                Dirty: true,
                Items:
                [
                    new IpcBuildDirtyStateItem(
                        ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                        "Assets/Scenes/Main.unity"),
                ]),
            SerializerOptions));
        using var inputProbe = JsonDocument.Parse(JsonSerializer.Serialize(
            new IpcBuildInputProbe(
                BuildTarget: "standaloneLinux64",
                UnityBuildTarget: "StandaloneLinux64",
                UnityBuildTargetGroup: "Standalone",
                SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit),
                Scenes: ["Assets/Scenes/Main.unity"],
                BuildOptions: "Development"),
            SerializerOptions));
        using var lifecycle = JsonDocument.Parse(JsonSerializer.Serialize(
            new IpcBuildLifecycleSnapshot(
                ServerVersion: "1.2.3",
                EditorMode: "batchmode",
                UnityVersion: "6000.0.0f1",
                ProjectFingerprint: "project-fingerprint",
                LifecycleState: IpcEditorLifecycleStateCodec.CompileFailed,
                BlockingReason: IpcEditorBlockingReasonCodec.CompileFailed,
                CompileState: IpcCompileStateCodec.Failed,
                CompileGeneration: "compile-1",
                DomainReloadGeneration: "domain-1",
                CanAcceptExecutionRequests: false,
                ObservedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
                PrimaryDiagnostic: new IpcPrimaryDiagnostic(
                    Kind: "compiler",
                    Code: "CS1002",
                    File: "Assets/Broken.cs",
                    Line: 4,
                    Column: 16,
                    Message: "; expected"),
                PlayMode: new IpcPlayModeSnapshot(
                    State: "stopped",
                    Transition: "none",
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false,
                    Generation: "play-1"),
                AssetRefreshGeneration: "asset-1"),
            SerializerOptions));

        JsonAssert.For(dirtyState.RootElement)
            .HasBoolean("checked", true)
            .HasBoolean("dirty", true)
            .HasArrayLength("items", 1)
            .HasProperty("items", 0, item => item
                .HasString("kind", ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene))
                .HasString("path", "Assets/Scenes/Main.unity"));
        JsonAssert.For(inputProbe.RootElement)
            .HasString("buildTarget", "standaloneLinux64")
            .HasString("unityBuildTarget", "StandaloneLinux64")
            .HasString("unityBuildTargetGroup", "Standalone")
            .HasString("sceneSource", ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit))
            .HasArrayLength("scenes", 1)
            .HasProperty("scenes", 0, scene => scene
                .HasString("Assets/Scenes/Main.unity"))
            .HasString("buildOptions", "Development");
        JsonAssert.For(lifecycle.RootElement)
            .HasString("serverVersion", "1.2.3")
            .HasString("editorMode", "batchmode")
            .HasString("unityVersion", "6000.0.0f1")
            .HasString("projectFingerprint", "project-fingerprint")
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.CompileFailed)
            .HasString("blockingReason", IpcEditorBlockingReasonCodec.CompileFailed)
            .HasString("compileState", IpcCompileStateCodec.Failed)
            .HasString("compileGeneration", "compile-1")
            .HasString("domainReloadGeneration", "domain-1")
            .HasString("assetRefreshGeneration", "asset-1")
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasString("observedAtUtc", "2026-06-12T00:00:00+00:00")
            .HasString("actionRequired", DaemonDiagnosisActionRequiredValues.FixCompileErrors)
            .HasProperty("primaryDiagnostic", diagnostic => diagnostic
                .HasString("kind", "compiler")
                .HasString("code", "CS1002")
                .HasString("file", "Assets/Broken.cs")
                .HasInt32("line", 4)
                .HasInt32("column", 16)
                .HasString("message", "; expected"))
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "stopped")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", false)
                .HasBoolean("isPlayingOrWillChangePlaymode", false)
                .HasString("generation", "play-1"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunContracts_SerializeWithCamelCaseFields ()
    {
        Assert.Equal("succeeded", ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded));
        Assert.Equal("failed", ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed));
        Assert.Equal("canceled", ContractLiteralCodec.ToValue(IpcBuildReportResult.Canceled));
        Assert.Equal("unknown", ContractLiteralCodec.ToValue(IpcBuildReportResult.Unknown));
        Assert.Equal("completed", ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed));
        Assert.Equal("failed", ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed));
        Assert.Equal("canceled", ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Canceled));

        using var request = JsonDocument.Parse(JsonSerializer.Serialize(
            new IpcBuildRunRequest(
                RunId: "build-run-1",
                BuildTarget: "standaloneLinux64",
                UnityBuildTarget: "StandaloneLinux64",
                SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit),
                ScenePaths: ["Assets/Scenes/Main.unity"],
                Development: true,
                OutputPath: "/tmp/ucli/output",
                OutputLayout: new IpcBuildOutputLayout(
                    Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                    LocationPathName: "/tmp/ucli/output/player/Player"),
                BuildReportPath: "/tmp/ucli/build-report.json",
                BuildLogPath: "/tmp/ucli/build.log")
            {
                TimeoutMilliseconds = 1234,
            },
            SerializerOptions));
        using var response = JsonDocument.Parse(JsonSerializer.Serialize(
            new IpcBuildRunResponse(
                RunId: "build-run-1",
                ProjectFingerprint: "project-fingerprint",
                LifecycleBefore: CreateBuildLifecycleSnapshot("before", canAcceptExecutionRequests: true),
                LifecycleAfter: CreateBuildLifecycleSnapshot("after", canAcceptExecutionRequests: true),
                DirtyState: new IpcBuildDirtyState(Checked: true, Dirty: false, Items: []),
                Input: new IpcBuildInputProbe(
                    BuildTarget: "standaloneLinux64",
                    UnityBuildTarget: "StandaloneLinux64",
                    UnityBuildTargetGroup: "Standalone",
                    SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit),
                    Scenes: ["Assets/Scenes/Main.unity"],
                    BuildOptions: "Development"),
                Report: new IpcBuildReportArtifact(
                    SchemaVersion: 1,
                    Result: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    UnityBuildTarget: "StandaloneLinux64",
                    OutputPath: "/tmp/ucli/output/build",
                    DurationMilliseconds: 2500,
                    TotalSizeBytes: 4096,
                    ErrorCount: 0,
                    WarningCount: 1,
                    Steps:
                    [
                        new IpcBuildReportStep(
                            Name: "Build player",
                            DurationMilliseconds: 2500,
                            Depth: 0,
                            MessageCount: 1),
                    ],
                    Messages:
                    [
                        new IpcBuildReportMessage(
                            Type: "warning",
                            Content: "Sample warning"),
                    ]),
                Logs: new IpcBuildLogSummary(
                    EntryCount: 3,
                    ErrorCount: 0,
                    WarningCount: 1,
                    CompletionReason: ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                    Window: new IpcBuildLogWindow(
                        StartedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                        CompletedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:03+00:00")))),
            SerializerOptions));

        JsonAssert.For(request.RootElement)
            .HasString("runId", "build-run-1")
            .HasString("buildTarget", "standaloneLinux64")
            .HasString("unityBuildTarget", "StandaloneLinux64")
            .HasString("sceneSource", ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit))
            .HasArrayLength("scenePaths", 1)
            .HasProperty("scenePaths", 0, scene => scene
                .HasString("Assets/Scenes/Main.unity"))
            .HasBoolean("development", true)
            .HasString("outputPath", "/tmp/ucli/output")
            .HasProperty("outputLayout", outputLayout => outputLayout
                .HasString("shape", ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File))
                .HasString("locationPathName", "/tmp/ucli/output/player/Player"))
            .HasString("buildReportPath", "/tmp/ucli/build-report.json")
            .HasString("buildLogPath", "/tmp/ucli/build.log")
            .HasInt32("timeoutMilliseconds", 1234);
        JsonAssert.For(response.RootElement)
            .HasString("runId", "build-run-1")
            .HasString("projectFingerprint", "project-fingerprint")
            .HasProperty("lifecycleBefore", lifecycle => lifecycle
                .HasString("compileGeneration", "compile-before")
                .HasString("domainReloadGeneration", "domain-before")
                .HasString("assetRefreshGeneration", "asset-before")
                .HasBoolean("canAcceptExecutionRequests", true))
            .HasProperty("lifecycleAfter", lifecycle => lifecycle
                .HasString("compileGeneration", "compile-after")
                .HasString("domainReloadGeneration", "domain-after")
                .HasString("assetRefreshGeneration", "asset-after"))
            .HasProperty("dirtyState", dirty => dirty
                .HasBoolean("checked", true)
                .HasBoolean("dirty", false)
                .HasArrayLength("items", 0))
            .HasProperty("input", input => input
                .HasString("buildTarget", "standaloneLinux64")
                .HasString("sceneSource", ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit)))
            .HasProperty("report", report => report
                .HasInt32("schemaVersion", 1)
                .HasString("result", ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded))
                .HasString("unityBuildTarget", "StandaloneLinux64")
                .HasString("outputPath", "/tmp/ucli/output/build")
                .HasInt32("durationMilliseconds", 2500)
                .HasInt32("totalSizeBytes", 4096)
                .HasInt32("errorCount", 0)
                .HasInt32("warningCount", 1)
                .HasArrayLength("steps", 1)
                .HasProperty("steps", 0, step => step
                    .HasString("name", "Build player")
                    .HasInt32("durationMilliseconds", 2500)
                    .HasInt32("depth", 0)
                    .HasInt32("messageCount", 1))
                .HasArrayLength("messages", 1)
                .HasProperty("messages", 0, message => message
                    .HasString("type", "warning")
                    .HasString("content", "Sample warning")))
            .HasProperty("logs", logs => logs
                .HasInt32("entryCount", 3)
                .HasInt32("errorCount", 0)
                .HasInt32("warningCount", 1)
                .HasString("completionReason", ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed))
                .HasProperty("window", window => window
                    .HasString("startedAtUtc", "2026-06-12T00:00:00+00:00")
                    .HasString("completedAtUtc", "2026-06-12T00:00:03+00:00")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunProgressContracts_SerializeWithCamelCaseFields ()
    {
        using var started = JsonDocument.Parse(JsonSerializer.Serialize(
            new TestCaseStartedEntry(
                "run-1",
                "test-1",
                "CanPass",
                "Assembly.Tests",
                "editmode",
                ["smoke"]),
            SerializerOptions));
        using var finished = JsonDocument.Parse(JsonSerializer.Serialize(
            new TestCaseFinishedEntry(
                "run-1",
                "test-1",
                "CanPass",
                "Assembly.Tests",
                "editmode",
                ["smoke"],
                "pass",
                12,
                Message: null,
                StackTrace: null),
            SerializerOptions));
        using var diagnostic = JsonDocument.Parse(JsonSerializer.Serialize(
            new TestRunDiagnosticEntry(
                "run-1",
                "TEST_PROGRESS_DROPPED",
                "Some progress entries were dropped.",
                "warning"),
            SerializerOptions));

        JsonAssert.For(started.RootElement)
            .HasString("runId", "run-1")
            .HasString("testId", "test-1")
            .HasString("testName", "CanPass")
            .HasString("assemblyName", "Assembly.Tests")
            .HasString("testPlatform", "editmode")
            .HasArrayLength("categories", 1);
        JsonAssert.For(finished.RootElement)
            .HasString("result", "pass")
            .HasInt32("durationMilliseconds", 12)
            .HasValueKind("message", JsonValueKind.Null)
            .HasValueKind("stackTrace", JsonValueKind.Null);
        JsonAssert.For(diagnostic.RootElement)
            .HasString("code", "TEST_PROGRESS_DROPPED")
            .HasString("message", "Some progress entries were dropped.")
            .HasString("severity", "warning");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DaemonStartSupervisorProgressContracts_SerializeWithCamelCaseFields ()
    {
        Assert.Equal("daemon.start.launching", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Launching));
        Assert.Equal("daemon.start.waitingForEndpoint", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint));
        Assert.Equal("daemon.start.blockerDetected", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.BlockerDetected));
        Assert.Equal("daemon.start.sessionRegistered", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SessionRegistered));
        Assert.Equal("daemon.start.endpointRegistered", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EndpointRegistered));
        Assert.Equal("daemon.start.lifecycleObserved", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved));
        Assert.Equal("startupObservation", ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation));
        Assert.Equal("lifecycleSnapshot", ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.LifecycleSnapshot));
        AssertPayloadKind(DaemonStartProgressEvent.Launching, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.WaitingForEndpoint, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.BlockerDetected, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.SessionRegistered, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.EndpointRegistered, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.LifecycleObserved, DaemonStartProgressPayloadKind.LifecycleSnapshot);
        Assert.False(DaemonStartProgressPayloadContract.TryGetPayloadKind(
            DaemonStartProgressEvent.Completed,
            out _));

        using var startupObservation = JsonDocument.Parse(JsonSerializer.Serialize(
            new DaemonStartStartupObservationProgressEntry(
                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
                "project-fingerprint",
                120000,
                ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                ContractLiteralCodec.ToValue(DaemonStartupBlockedProcessPolicy.Terminate),
                "attempt-1",
                ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
                true,
                1234,
                DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
                ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
                ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile),
                ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.ScriptCompilation),
                ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix),
                "Unity scripts failed to compile.",
                "UNITY_SCRIPT_COMPILATION_FAILED"),
            SerializerOptions));
        using var lifecycleSnapshot = JsonDocument.Parse(JsonSerializer.Serialize(
            new DaemonStartLifecycleSnapshotProgressEntry(
                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.LifecycleSnapshot),
                "project-fingerprint",
                120000,
                ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                ContractLiteralCodec.ToValue(DaemonStartupBlockedProcessPolicy.Terminate),
                IpcEditorLifecycleStateCodec.Ready,
                null,
                true),
            SerializerOptions));

        JsonAssert.For(startupObservation.RootElement)
            .HasString("payloadKind", "startupObservation")
            .HasString("projectFingerprint", "project-fingerprint")
            .HasInt32("timeoutMilliseconds", 120000)
            .HasString("editorMode", "batchmode")
            .HasString("onStartupBlocked", "terminate")
            .HasString("launchAttemptId", "attempt-1")
            .HasString("ownerKind", "cli")
            .HasBoolean("canShutdownProcess", true)
            .HasInt32("processId", 1234)
            .HasString("processStartedAtUtc", "2026-05-21T00:00:00+00:00")
            .HasString("startupStatus", "blocked")
            .HasString("startupBlockingReason", "compile")
            .HasString("startupPhase", "scriptCompilation")
            .HasString("retryDisposition", "retryAfterFix")
            .HasString("message", "Unity scripts failed to compile.")
            .HasString("errorCode", "UNITY_SCRIPT_COMPILATION_FAILED");
        Assert.False(startupObservation.RootElement.TryGetProperty("lifecycleState", out _));
        Assert.False(startupObservation.RootElement.TryGetProperty("blockingReason", out _));
        Assert.False(startupObservation.RootElement.TryGetProperty("canAcceptExecutionRequests", out _));

        JsonAssert.For(lifecycleSnapshot.RootElement)
            .HasString("payloadKind", "lifecycleSnapshot")
            .HasString("projectFingerprint", "project-fingerprint")
            .HasInt32("timeoutMilliseconds", 120000)
            .HasString("editorMode", "batchmode")
            .HasString("onStartupBlocked", "terminate")
            .HasString("lifecycleState", "ready")
            .HasValueKind("blockingReason", JsonValueKind.Null)
            .HasBoolean("canAcceptExecutionRequests", true);
    }

    private static void AssertPayloadKind (
        DaemonStartProgressEvent progressEvent,
        DaemonStartProgressPayloadKind expectedPayloadKind)
    {
        Assert.True(DaemonStartProgressPayloadContract.TryGetPayloadKind(progressEvent, out var payloadKind));
        Assert.Equal(expectedPayloadKind, payloadKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliCode_RetainsUnknownCodeValue ()
    {
        UcliCode code = new("FUTURE_DAEMON_FAILURE");

        Assert.Equal("FUTURE_DAEMON_FAILURE", code.Value);
        Assert.Equal("FUTURE_DAEMON_FAILURE", code.ToString());
        string rawValue = code;
        Assert.Equal("FUTURE_DAEMON_FAILURE", rawValue);
        Assert.Equal(new UcliCode("FUTURE_DAEMON_FAILURE"), code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UcliCode_RejectsBlankValue (string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new UcliCode(value!));
        Assert.False(UcliCode.TryCreate(value, out _));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("lowercase_code")]
    [InlineData("CODE-WITH-HYPHEN")]
    [InlineData("1_CODE")]
    [InlineData("CODE.")]
    public void UcliCode_RejectsInvalidMachineToken (string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new UcliCode(value));
        Assert.False(UcliCode.TryCreate(value, out _));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"protocolVersion":1,"requestId":"req","status":"error","payload":{},"errors":[{"code":null,"message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"req","status":"error","payload":{},"errors":[{"code":123,"message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"req","status":"error","payload":{},"errors":[{"code":"","message":"bad","opId":null}]}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"req","status":"error","payload":{},"errors":[{"code":"lowercase_code","message":"bad","opId":null}]}""")]
    public void IpcResponse_WhenErrorCodeJsonIsInvalid_Throws (string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IpcResponse>(json, SerializerOptions));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPayloadCodec_SemanticStringValue_RoundTripsAsJsonString ()
    {
        using var document = JsonDocument.Parse("{\"path\":\"Assets/Scenes/Main.unity\"}");

        var result = IpcPayloadCodec.TryDeserialize<ScenePathArgs>(
            document.RootElement,
            out var args,
            out var error);

        Assert.True(result, error.Message);
        Assert.Equal("Assets/Scenes/Main.unity", args.Path.Value);

        var payload = IpcPayloadCodec.SerializeToElement(args);

        JsonAssert.For(payload)
            .HasString("path", "Assets/Scenes/Main.unity");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPayloadCodec_ReferenceSemanticStringValues_RoundTripAsJsonStrings ()
    {
        using var document = JsonDocument.Parse("{\"var\":\"created\",\"assetGuid\":\"11111111111111111111111111111111\"}");

        var result = IpcPayloadCodec.TryDeserialize<AssetReferenceArgs>(
            document.RootElement,
            out var args,
            out var error);

        Assert.True(result, error.Message);
        Assert.Equal("created", args.Alias!.Value);
        Assert.Equal("11111111111111111111111111111111", args.AssetGuid!.Value);

        var payload = IpcPayloadCodec.SerializeToElement(args);

        JsonAssert.For(payload)
            .HasString("var", "created")
            .HasString("assetGuid", "11111111111111111111111111111111");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ErrorCodeDefinitions_ExposeKnownCoreAndReadIndexCodes ()
    {
        Assert.Equal("INVALID_ARGUMENT", UcliCoreErrorCodes.InvalidArgument.Value);
        Assert.Equal("NOT_INITIALIZED", UcliCoreErrorCodes.NotInitialized.Value);
        Assert.Equal("SESSION_TOKEN_REQUIRED", IpcSessionErrorCodes.SessionTokenRequired.Value);
        Assert.Equal("SESSION_TOKEN_INVALID", IpcSessionErrorCodes.SessionTokenInvalid.Value);
        Assert.Equal("READ_INDEX_BOOTSTRAP_FAILED", ReadIndexErrorCodes.ReadIndexBootstrapFailed.Value);
        Assert.Equal("READ_INDEX_FORMAT_INVALID", ReadIndexErrorCodes.ReadIndexFormatInvalid.Value);
        Assert.Equal("READ_INDEX_FRESH_REQUIRED", ReadIndexErrorCodes.ReadIndexFreshRequired.Value);
        Assert.Equal("PLAN_TOKEN_REQUIRED", PlanTokenErrorCodes.PlanTokenRequired.Value);
        Assert.Equal("PLAN_TOKEN_INVALID", PlanTokenErrorCodes.PlanTokenInvalid.Value);
        Assert.Equal("PLAN_TOKEN_EXPIRED", PlanTokenErrorCodes.PlanTokenExpired.Value);
        Assert.Equal("PLAN_TOKEN_REQUEST_MISMATCH", PlanTokenErrorCodes.PlanTokenRequestMismatch.Value);
        Assert.Equal("STATE_CHANGED_SINCE_PLAN", PlanTokenErrorCodes.StateChangedSincePlan.Value);
        Assert.Equal("REQUEST_ID_CONFLICT", ExecuteRequestErrorCodes.RequestIdConflict.Value);
        Assert.Equal("OPERATION_CONTRACT_VIOLATION", ExecuteRequestErrorCodes.OperationContractViolation.Value);
        Assert.Equal("EDITOR_STARTING", EditorLifecycleErrorCodes.EditorStarting.Value);
        Assert.Equal("EDITOR_BUSY", EditorLifecycleErrorCodes.EditorBusy.Value);
        Assert.Equal("EDITOR_COMPILING", EditorLifecycleErrorCodes.EditorCompiling.Value);
        Assert.Equal("EDITOR_DOMAIN_RELOADING", EditorLifecycleErrorCodes.EditorDomainReloading.Value);
        Assert.Equal("EDITOR_PLAYMODE", EditorLifecycleErrorCodes.EditorPlaymode.Value);
        Assert.Equal("EDITOR_MODAL_BLOCKED", EditorLifecycleErrorCodes.EditorModalBlocked.Value);
        Assert.Equal("EDITOR_SAFE_MODE", EditorLifecycleErrorCodes.EditorSafeMode.Value);
        Assert.Equal("EDITOR_SHUTTING_DOWN", EditorLifecycleErrorCodes.EditorShuttingDown.Value);
        Assert.Equal("PLAYMODE_NOT_ACTIVE", PlayModeErrorCodes.PlayModeNotActive.Value);
        Assert.Equal("PLAYMODE_REQUIRES_GUI_EDITOR", PlayModeErrorCodes.PlayModeRequiresGuiEditor.Value);
        Assert.Equal("PLAYMODE_PERSISTENCE_FORBIDDEN", PlayModeErrorCodes.PlayModePersistenceForbidden.Value);
        Assert.Equal("DAEMON_EDITOR_MODE_MISMATCH", DaemonErrorCodes.DaemonEditorModeMismatch.Value);
        Assert.Equal("INTERNAL_ERROR", UcliCoreErrorCodes.InternalError.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteRequest_SerializesOptionalExecutionControlsOnlyWhenSpecified ()
    {
        var requestWithToken = new IpcExecuteRequest(
            Command: UcliCommandIds.Call,
            Arguments: JsonSerializer.SerializeToElement(new
            {
                protocolVersion = 1,
                requestId = "req-1",
                steps = Array.Empty<object>(),
            }))
        {
            PlanToken = "token-value",
            AllowDangerous = true,
            AllowPlayMode = true,
        };

        var withTokenJson = JsonSerializer.SerializeToElement(requestWithToken, SerializerOptions);
        Assert.True(withTokenJson.TryGetProperty("planToken", out var planTokenElement));
        Assert.Equal("token-value", planTokenElement.GetString());
        Assert.True(withTokenJson.TryGetProperty("allowDangerous", out var allowDangerousElement));
        Assert.True(allowDangerousElement.GetBoolean());
        Assert.True(withTokenJson.TryGetProperty("allowPlayMode", out var allowPlayModeElement));
        Assert.True(allowPlayModeElement.GetBoolean());
        Assert.False(withTokenJson.TryGetProperty("failFast", out _));

        var requestWithoutToken = new IpcExecuteRequest(
            Command: UcliCommandIds.Plan,
            Arguments: JsonSerializer.SerializeToElement(new
            {
                protocolVersion = 1,
                requestId = "req-1",
                steps = Array.Empty<object>(),
            }))
        {
            FailFast = true,
        };
        var withoutTokenJson = JsonSerializer.SerializeToElement(requestWithoutToken, SerializerOptions);
        Assert.False(withoutTokenJson.TryGetProperty("planToken", out _));
        Assert.False(withoutTokenJson.TryGetProperty("allowDangerous", out _));
        Assert.False(withoutTokenJson.TryGetProperty("allowPlayMode", out _));
        Assert.True(withoutTokenJson.TryGetProperty("failFast", out var failFastElement));
        Assert.True(failFastElement.GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPingRequest_SerializesFailFastOnlyWhenSpecified ()
    {
        var defaultRequest = new IpcPingRequest(IpcPingClientVersions.OneshotStartup);
        var defaultJson = JsonSerializer.SerializeToElement(defaultRequest, SerializerOptions);

        Assert.Equal(IpcPingClientVersions.OneshotStartup, defaultJson.GetProperty("clientVersion").GetString());
        Assert.False(defaultJson.TryGetProperty("failFast", out _));

        var failFastRequest = new IpcPingRequest(IpcPingClientVersions.Ready, FailFast: true);
        var failFastJson = JsonSerializer.SerializeToElement(failFastRequest, SerializerOptions);

        Assert.Equal(IpcPingClientVersions.Ready, failFastJson.GetProperty("clientVersion").GetString());
        Assert.True(failFastJson.GetProperty("failFast").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPingResponse_SerializesPlayModeSnapshotWithCamelCaseFields ()
    {
        var response = new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: IpcCompileStateCodec.Ready,
            LifecycleState: IpcEditorLifecycleStateCodec.Playmode,
            BlockingReason: IpcEditorBlockingReasonCodec.PlayMode,
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: false,
            PlayMode: new IpcPlayModeSnapshot(
                State: "playing",
                Transition: "none",
                IsPlaying: true,
                IsPlayingOrWillChangePlaymode: true,
                Generation: "42"));

        var json = JsonSerializer.SerializeToElement(response, SerializerOptions);

        JsonAssert.For(json)
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Playmode)
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "playing")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", true)
                .HasBoolean("isPlayingOrWillChangePlaymode", true)
                .HasString("generation", "42"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcOpsReadRequest_SerializesEditLoweringCatalogFlagOnlyWhenSpecified ()
    {
        var defaultRequest = new IpcOpsReadRequest();
        var defaultJson = JsonSerializer.SerializeToElement(defaultRequest, SerializerOptions);

        Assert.False(defaultJson.TryGetProperty("includeEditLoweringOnly", out _));

        var validationRequest = new IpcOpsReadRequest(
            FailFast: true,
            RequireReadinessGate: true,
            IncludeEditLoweringOnly: true);
        var validationJson = JsonSerializer.SerializeToElement(validationRequest, SerializerOptions);

        Assert.True(validationJson.GetProperty("failFast").GetBoolean());
        Assert.True(validationJson.GetProperty("requireReadinessGate").GetBoolean());
        Assert.True(validationJson.GetProperty("includeEditLoweringOnly").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcMethodNames_ExposeExpectedMethodLiterals ()
    {
        Assert.Equal("ping", IpcMethodNames.Ping);
        Assert.Equal("execute", IpcMethodNames.Execute);
        Assert.Equal("ops.read", IpcMethodNames.OpsRead);
        Assert.Equal("index.assets.read", IpcMethodNames.IndexAssetsRead);
        Assert.Equal("index.scene-tree-lite.read", IpcMethodNames.IndexSceneTreeLiteRead);
        Assert.Equal("test.run", IpcMethodNames.TestRun);
        Assert.Equal("compile", IpcMethodNames.Compile);
        Assert.Equal("shutdown", IpcMethodNames.Shutdown);
        Assert.Equal("daemon.logs.read", IpcMethodNames.DaemonLogsRead);
        Assert.Equal("unity.logs.read", IpcMethodNames.UnityLogsRead);
        Assert.Equal("unity.console.clear", IpcMethodNames.UnityConsoleClear);
        Assert.Equal("play.status", IpcMethodNames.PlayStatus);
        Assert.Equal("play.enter", IpcMethodNames.PlayEnter);
        Assert.Equal("play.exit", IpcMethodNames.PlayExit);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayRequestContracts_SerializeWithCamelCaseFields ()
    {
        var statusRequest = JsonSerializer.SerializeToElement(new IpcPlayStatusRequest(), SerializerOptions);
        var enterRequest = JsonSerializer.SerializeToElement(
            new IpcPlayEnterRequest { TimeoutMilliseconds = 1500 },
            SerializerOptions);
        var exitRequest = JsonSerializer.SerializeToElement(new IpcPlayExitRequest(), SerializerOptions);

        Assert.Equal(JsonValueKind.Object, statusRequest.ValueKind);
        Assert.Empty(statusRequest.EnumerateObject());
        JsonAssert.For(enterRequest)
            .HasInt32("timeoutMilliseconds", 1500);
        Assert.False(exitRequest.TryGetProperty("timeoutMilliseconds", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayResponseContracts_SerializeWithCamelCaseFields ()
    {
        var before = CreatePlayLifecycleSnapshot("stopped", "none");
        var after = CreatePlayLifecycleSnapshot("playing", "none");
        var statusResponse = new IpcPlayStatusResponse(before);
        var transitionResponse = new IpcPlayTransitionResponse(
            new IpcPlayTransitionResult(
                Transition: IpcPlayTransitionCommandNames.Enter,
                Result: IpcPlayTransitionResultNames.Entered,
                Before: before)
            {
                After = after,
                ApplicationState = IpcPlayApplicationStateNames.Applied,
            });

        using var statusDocument = JsonDocument.Parse(JsonSerializer.Serialize(statusResponse, SerializerOptions));
        using var transitionDocument = JsonDocument.Parse(JsonSerializer.Serialize(transitionResponse, SerializerOptions));

        JsonAssert.For(statusDocument.RootElement)
            .HasProperty("snapshot", snapshot => snapshot
                .HasString("serverVersion", "0.5.0")
                .HasString("editorMode", "gui")
                .HasString("unityVersion", "6000.1.4f1")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasString("lifecycleState", "ready")
                .HasString("blockingReason", "none")
                .HasString("compileState", "idle")
                .HasBoolean("canAcceptExecutionRequests", true)
                .HasString("observedAtUtc", "2026-05-21T00:00:00+00:00")
                .HasProperty("playMode", playMode => playMode
                    .HasString("state", "stopped")
                    .HasString("transition", "none")
                    .HasBoolean("isPlaying", false)
                    .HasBoolean("isPlayingOrWillChangePlaymode", false)
                    .HasString("generation", "42")));

        JsonAssert.For(transitionDocument.RootElement)
            .HasProperty("transition", transition => transition
                .HasString("transition", IpcPlayTransitionCommandNames.Enter)
                .HasString("result", IpcPlayTransitionResultNames.Entered)
                .HasString("applicationState", IpcPlayApplicationStateNames.Applied)
                .HasProperty("before", beforeSnapshot => beforeSnapshot
                    .HasProperty("playMode", playMode => playMode
                        .HasString("state", "stopped")))
                .HasProperty("after", afterSnapshot => afterSnapshot
                    .HasProperty("playMode", playMode => playMode
                        .HasString("state", "playing"))));

        var roundTrip = JsonSerializer.Deserialize<IpcPlayTransitionResponse>(
            transitionDocument.RootElement.GetRawText(),
            SerializerOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(IpcPlayTransitionCommandNames.Enter, roundTrip.Transition.Transition);
        Assert.Equal(IpcPlayApplicationStateNames.Applied, roundTrip.Transition.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayLiteralContracts_ExposeExpectedLiterals ()
    {
        Assert.Equal(0, (int)IpcPlayModeState.Stopped);
        Assert.Equal(1, (int)IpcPlayModeState.Entering);
        Assert.Equal(2, (int)IpcPlayModeState.Playing);
        Assert.Equal(3, (int)IpcPlayModeState.Exiting);
        Assert.Equal(4, (int)IpcPlayModeState.Unknown);
        Assert.Equal(0, (int)IpcPlayModeTransition.None);
        Assert.Equal(1, (int)IpcPlayModeTransition.Entering);
        Assert.Equal(2, (int)IpcPlayModeTransition.Exiting);
        Assert.Equal("stopped", ContractLiteralCodec.ToValue(IpcPlayModeState.Stopped));
        Assert.Equal("entering", ContractLiteralCodec.ToValue(IpcPlayModeState.Entering));
        Assert.Equal("playing", ContractLiteralCodec.ToValue(IpcPlayModeState.Playing));
        Assert.Equal("exiting", ContractLiteralCodec.ToValue(IpcPlayModeState.Exiting));
        Assert.Equal("unknown", ContractLiteralCodec.ToValue(IpcPlayModeState.Unknown));
        Assert.Equal("none", ContractLiteralCodec.ToValue(IpcPlayModeTransition.None));
        Assert.Equal("entering", ContractLiteralCodec.ToValue(IpcPlayModeTransition.Entering));
        Assert.Equal("exiting", ContractLiteralCodec.ToValue(IpcPlayModeTransition.Exiting));
        Assert.Equal("enter", IpcPlayTransitionCommandNames.Enter);
        Assert.Equal("exit", IpcPlayTransitionCommandNames.Exit);
        Assert.Equal("entered", IpcPlayTransitionResultNames.Entered);
        Assert.Equal("alreadyEntered", IpcPlayTransitionResultNames.AlreadyEntered);
        Assert.Equal("exited", IpcPlayTransitionResultNames.Exited);
        Assert.Equal("alreadyExited", IpcPlayTransitionResultNames.AlreadyExited);
        Assert.Equal("timeout", IpcPlayTransitionResultNames.Timeout);
        Assert.Equal("blocked", IpcPlayTransitionResultNames.Blocked);
        Assert.Equal("notApplied", IpcPlayApplicationStateNames.NotApplied);
        Assert.Equal("applied", IpcPlayApplicationStateNames.Applied);
        Assert.Equal("indeterminate", IpcPlayApplicationStateNames.Indeterminate);
        Assert.Equal("unknown", IpcPlayApplicationStateNames.Unknown);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayModeCodecs_RoundTripStableLiterals ()
    {
        Assert.True(ContractLiteralInputParser.TryParseTrimmed<IpcPlayModeState>(" playing ", out var state));
        Assert.Equal(IpcPlayModeState.Playing, state);
        Assert.Equal("playing", ContractLiteralCodec.ToValue(state));
        Assert.False(ContractLiteralInputParser.IsDefinedTrimmed<IpcPlayModeState>("unsupported"));

        Assert.True(ContractLiteralInputParser.TryParseTrimmed<IpcPlayModeTransition>(" none ", out var transition));
        Assert.Equal(IpcPlayModeTransition.None, transition);
        Assert.Equal("none", ContractLiteralCodec.ToValue(transition));
        Assert.False(ContractLiteralInputParser.IsDefinedTrimmed<IpcPlayModeTransition>("unsupported"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcOpsReadContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcOpsReadRequest(FailFast: true, RequireReadinessGate: true);
        var describe = CreateGoDescribeContract();
        var responsePayload = new IpcOpsReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            Operations:
            [
                new IndexOpEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}""",
                    ResultSchemaJson: """{"type":"object"}""")
                {
                    Description = describe.Description,
                    Inputs = describe.Inputs,
                    ResultContract = describe.ResultContract,
                    Assurance = describe.Assurance,
                },
            ]);

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasBoolean("failFast", true)
            .HasBoolean("requireReadinessGate", true);
        JsonAssert.For(responseDocument.RootElement)
            .HasString("generatedAtUtc", "2026-03-06T00:00:00+00:00")
            .HasArrayLength("operations", 1)
            .HasProperty("operations", 0, operation => operation
                .HasString("name", UcliPrimitiveOperationNames.GoDescribe)
                .HasString("kind", "query")
                .HasString("policy", "safe")
                .HasString("description", describe.Description!)
                .HasProperty("resultContract", resultContract => resultContract
                    .HasBoolean("emitted", true)
                    .HasString("resultType", "GameObjectDescriptionResult"))
                .HasProperty("assurance", assurance => assurance
                    .HasBoolean("mayDirty", false)
                    .HasBoolean("mayPersist", false)
                    .HasString("planMode", "observesLiveUnity"))
                .HasString("argsSchemaJson", """{"type":"object"}"""));

        var operationElement = responseDocument.RootElement.GetProperty("operations")[0];
        var targetInputElement = operationElement.GetProperty("inputs").EnumerateArray().Single(input =>
            string.Equals(input.GetProperty("name").GetString(), "target", StringComparison.Ordinal));
        var globalObjectIdVariantElement = targetInputElement.GetProperty("variants").EnumerateArray().Single(variant =>
            string.Equals(variant.GetProperty("name").GetString(), "byGlobalObjectId", StringComparison.Ordinal));
        var fieldElement = Assert.Single(globalObjectIdVariantElement.GetProperty("fields").EnumerateArray());

        Assert.False(globalObjectIdVariantElement.TryGetProperty("argsPaths", out _));
        Assert.False(globalObjectIdVariantElement.TryGetProperty("constraints", out _));
        JsonAssert.For(fieldElement)
            .HasString("name", "globalObjectId")
            .HasString("argsPath", "$.target.globalObjectId")
            .HasString("description", "Resolved Unity GlobalObjectId.")
            .HasArrayLength("constraints", 1)
            .HasProperty("constraints", 0, constraint => constraint
                .HasString("kind", "globalObjectId"));

        var sceneHierarchyVariantElement = targetInputElement.GetProperty("variants").EnumerateArray().Single(variant =>
            string.Equals(variant.GetProperty("name").GetString(), "bySceneHierarchyPath", StringComparison.Ordinal));
        var sceneFieldElement = sceneHierarchyVariantElement.GetProperty("fields").EnumerateArray().Single(candidate =>
            string.Equals(candidate.GetProperty("name").GetString(), "scene", StringComparison.Ordinal));
        var hierarchyPathFieldElement = sceneHierarchyVariantElement.GetProperty("fields").EnumerateArray().Single(candidate =>
            string.Equals(candidate.GetProperty("name").GetString(), "hierarchyPath", StringComparison.Ordinal));

        Assert.False(sceneHierarchyVariantElement.TryGetProperty("argsPaths", out _));
        Assert.False(sceneHierarchyVariantElement.TryGetProperty("constraints", out _));
        JsonAssert.For(sceneFieldElement)
            .HasString("argsPath", "$.target.scene")
            .HasString("description", "Scene asset path for a hierarchy selector.");
        var assetExistsConstraint = sceneFieldElement.GetProperty("constraints").EnumerateArray().Single(constraint =>
            string.Equals(constraint.GetProperty("kind").GetString(), "assetExists", StringComparison.Ordinal));
        JsonAssert.For(assetExistsConstraint)
            .HasString("assetKind", "scene");
        JsonAssert.For(hierarchyPathFieldElement)
            .HasString("argsPath", "$.target.hierarchyPath")
            .HasString("description", "Unity hierarchy path inside the selected scene or prefab.");
        Assert.Contains(hierarchyPathFieldElement.GetProperty("constraints").EnumerateArray(), constraint =>
            string.Equals(constraint.GetProperty("kind").GetString(), "hierarchyPath", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcIndexAssetsReadContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcIndexAssetsReadRequest(FailFast: true);
        var responsePayload = new IpcIndexAssetsReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            AssetSearchEntries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Data/Spawner.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "Spawner",
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "Game.Spawner, Assembly-CSharp",
                        "UnityEngine.ScriptableObject, UnityEngine.CoreModule",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ],
            GuidPathEntries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "11111111111111111111111111111111",
                    AssetPath: "Assets/Data/Spawner.asset"),
            ]);

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasBoolean("failFast", true);
        JsonAssert.For(responseDocument.RootElement)
            .HasString("generatedAtUtc", "2026-03-06T00:00:00+00:00")
            .HasArrayLength("assetSearchEntries", 1)
            .HasArrayLength("guidPathEntries", 1)
            .HasProperty("assetSearchEntries", 0, entry => entry
                .HasString("assetPath", "Assets/Data/Spawner.asset")
                .HasString("assetGuid", "11111111111111111111111111111111")
                .HasString("name", "Spawner")
                .HasString("typeId", "Game.Spawner, Assembly-CSharp")
                .HasArrayLength("searchTypeIds", 3))
            .HasProperty("guidPathEntries", 0, entry => entry
                .HasString("assetGuid", "11111111111111111111111111111111")
                .HasString("assetPath", "Assets/Data/Spawner.asset"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcIndexSceneTreeLiteReadContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcIndexSceneTreeLiteReadRequest(ScenePath: "Assets/Scenes/Sample.unity");
        var responsePayload = new IpcIndexSceneTreeLiteReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(
                            name: "Child",
                            globalObjectId: string.Empty,
                            children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ],
            SourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.LoadedScene, isDirty: true));

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasString("scenePath", "Assets/Scenes/Sample.unity")
            .HasBoolean("failFast", false)
            .HasBoolean("loadedSceneOnly", false);
        JsonAssert.For(responseDocument.RootElement)
            .HasString("generatedAtUtc", "2026-03-06T00:00:00+00:00")
            .HasString("scenePath", "Assets/Scenes/Sample.unity")
            .HasProperty("sourceState", state => state
                .HasString("kind", "loadedScene")
                .HasBoolean("isDirty", true))
            .HasArrayLength("roots", 1)
            .HasProperty("roots", 0, node => node
                .HasString("name", "Root")
                .HasString("globalObjectId", "GlobalObjectId_V1-2-3-4-5-6")
                .HasString("childrenState", IndexSceneTreeLiteNodeChildrenStateValues.Complete)
                .HasArrayLength("children", 1));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BoundedQueryOperationContracts_SerializeWithCursorWindowFields ()
    {
        var cursor = BoundedWindowCursorCodec.Encode(1);
        var nextCursor = BoundedWindowCursorCodec.Encode(2);
        var assetsResult = new AssetsFindResult(
            matches:
            [
                new AssetsFindMatch(
                    assetPath: "Assets/Data/A.asset",
                    assetGuid: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    name: "A",
                    typeId: "UnityEngine.ScriptableObject, UnityEngine.CoreModule"),
            ],
            window: new BoundedWindow(
                limit: 1,
                cursor: cursor,
                nextCursor: nextCursor,
                isComplete: false,
                totalCount: 3));
        var sceneResult = new SceneTreeResult(
            path: new SceneAssetPath("Assets/Scenes/Main.unity"),
            roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-1-2-3-4-5",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ],
            sourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.ReadIndex, isDirty: false),
            window: new BoundedWindow(
                limit: 1,
                cursor: null,
                nextCursor: nextCursor,
                isComplete: false,
                totalCount: 2));

        var assetsElement = IpcPayloadCodec.SerializeToElement(assetsResult);
        var sceneElement = IpcPayloadCodec.SerializeToElement(sceneResult);

        JsonAssert.For(assetsElement)
            .HasArrayLength("matches", 1)
            .HasProperty("window", window => window
                .HasInt32("limit", 1)
                .HasString("cursor", cursor)
                .HasString("nextCursor", nextCursor)
                .HasBoolean("isComplete", false)
                .HasInt32("totalCount", 3));
        Assert.False(assetsElement.GetProperty("window").TryGetProperty("after", out _));
        JsonAssert.For(sceneElement)
            .HasArrayLength("roots", 1)
            .HasProperty("roots", 0, root => root
                .HasString("childrenState", IndexSceneTreeLiteNodeChildrenStateValues.Complete))
            .HasProperty("window", window => window
                .IsNull("cursor")
                .HasString("nextCursor", nextCursor));
        Assert.False(sceneElement.GetProperty("window").TryGetProperty("after", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SceneTreeNodeContract_SerializesUnknownChildrenState ()
    {
        var node = new IndexSceneTreeLiteNodeJsonContract(
            name: "Root",
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5",
            children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Unknown);

        var element = IpcPayloadCodec.SerializeToElement(node);

        JsonAssert.For(element)
            .HasString("childrenState", IndexSceneTreeLiteNodeChildrenStateValues.Unknown);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcShutdownContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcShutdownRequest(RequestedBy: "ucli-daemon-stop");
        var responsePayload = new IpcShutdownResponse(Accepted: true, Message: "Shutdown accepted.");

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasString("requestedBy", "ucli-daemon-stop");
        JsonAssert.For(responseDocument.RootElement)
            .HasBoolean("accepted", true)
            .HasString("message", "Shutdown accepted.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcTestRunRequest(
            TestPlatform: "editmode",
            TestFilter: null,
            TestCategories: Array.Empty<string>(),
            AssemblyNames: Array.Empty<string>(),
            TestSettingsPath: null,
            ResultsXmlPath: "/tmp/results.xml",
            EditorLogPath: "/tmp/editor.log",
            FailFast: true);
        var responsePayload = new IpcTestRunResponse(ExitCode: 2);

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasString("testPlatform", "editmode")
            .IsNull("testFilter")
            .HasArrayLength("testCategories", 0)
            .HasArrayLength("assemblyNames", 0)
            .IsNull("testSettingsPath")
            .HasString("resultsXmlPath", "/tmp/results.xml")
            .HasString("editorLogPath", "/tmp/editor.log")
            .HasBoolean("failFast", true);
        JsonAssert.For(responseDocument.RootElement)
            .HasInt32("exitCode", 2);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcCompileContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcCompileRequest("run-1")
        {
            TimeoutMilliseconds = 10000,
        };
        var responsePayload = new IpcCompileResponse(
            RunId: "run-1",
            Summary: CreateCompileSummary());

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasString("runId", "run-1")
            .HasInt32("timeoutMilliseconds", 10000);
        JsonAssert.For(responseDocument.RootElement)
            .HasString("runId", "run-1")
            .HasProperty("summary", summary => summary
                .HasString("runId", "run-1")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasBoolean("completed", true)
                .HasProperty("scriptCompilation", scriptCompilation => scriptCompilation
                    .HasProperty("diagnostics", diagnostics => diagnostics
                        .HasInt32("errorCount", 1)
                        .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                            .HasString("kind", "compiler")
                            .HasString("code", "CS1002")))));
        Assert.False(responseDocument.RootElement.TryGetProperty("summaryJsonPath", out _));
        Assert.False(responseDocument.RootElement.TryGetProperty("diagnosticsJsonPath", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileProgressContracts_SerializeWithCamelCaseFields ()
    {
        var startedPayload = new CompileStartedEntry(
            RunId: "run-1",
            ProjectFingerprint: "project-fingerprint",
            RequestedMode: "auto",
            ResolvedMode: "oneshot",
            SessionKind: "transientProbe",
            TimeoutMilliseconds: 10000);
        var refreshPayload = new CompileRefreshStartedEntry(
            RunId: "run-1",
            RefreshOrigin: "assetDatabaseRefresh",
            ObservationSource: "hostDispatch");
        var recoveredPayload = new CompileRecoveredEntry(
            RunId: "run-1",
            SummaryJsonPath: "/tmp/ucli/compile/run-1/summary.json",
            DispatchFailureCode: IpcTransportErrorCodes.IpcTimeout.Value,
            PollAttempts: 2);
        var diagnosticPayload = new CompileDiagnosticEntry(
            RunId: "run-1",
            RefreshOrigin: "diagnosticsRead",
            PrimaryDiagnostic: new IpcPrimaryDiagnostic(
                Kind: "compiler",
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected"));
        var completedPayload = new CompileCompletedEntry(
            RunId: "run-1",
            Verdict: "fail",
            ErrorCount: 1,
            WarningCount: 0,
            SummaryJsonPath: "/tmp/ucli/compile/run-1/summary.json",
            DiagnosticsJsonPath: "/tmp/ucli/compile/run-1/diagnostics.json");

        using var startedDocument = JsonDocument.Parse(JsonSerializer.Serialize(startedPayload, SerializerOptions));
        using var refreshDocument = JsonDocument.Parse(JsonSerializer.Serialize(refreshPayload, SerializerOptions));
        using var recoveredDocument = JsonDocument.Parse(JsonSerializer.Serialize(recoveredPayload, SerializerOptions));
        using var diagnosticDocument = JsonDocument.Parse(JsonSerializer.Serialize(diagnosticPayload, SerializerOptions));
        using var completedDocument = JsonDocument.Parse(JsonSerializer.Serialize(completedPayload, SerializerOptions));

        Assert.Equal("compile.started", CompileProgressEventNames.Started);
        Assert.Equal("compile.refresh.started", CompileProgressEventNames.RefreshStarted);
        Assert.Equal("compile.recovered", CompileProgressEventNames.Recovered);
        Assert.Equal("compile.diagnostic", CompileProgressEventNames.Diagnostic);
        Assert.Equal("compile.completed", CompileProgressEventNames.Completed);
        JsonAssert.For(startedDocument.RootElement)
            .HasString("runId", "run-1")
            .HasString("projectFingerprint", "project-fingerprint")
            .HasString("requestedMode", "auto")
            .HasString("resolvedMode", "oneshot")
            .HasString("sessionKind", "transientProbe")
            .HasInt32("timeoutMilliseconds", 10000);
        JsonAssert.For(refreshDocument.RootElement)
            .HasString("runId", "run-1")
            .HasString("refreshOrigin", "assetDatabaseRefresh")
            .HasString("observationSource", "hostDispatch");
        JsonAssert.For(recoveredDocument.RootElement)
            .HasString("runId", "run-1")
            .HasString("summaryJsonPath", "/tmp/ucli/compile/run-1/summary.json")
            .HasString("dispatchFailureCode", IpcTransportErrorCodes.IpcTimeout.Value)
            .HasInt32("pollAttempts", 2);
        JsonAssert.For(diagnosticDocument.RootElement)
            .HasString("runId", "run-1")
            .HasString("refreshOrigin", "diagnosticsRead")
            .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                .HasString("kind", "compiler")
                .HasString("code", "CS1002"));
        JsonAssert.For(completedDocument.RootElement)
            .HasString("runId", "run-1")
            .HasString("verdict", "fail")
            .HasInt32("errorCount", 1)
            .HasInt32("warningCount", 0)
            .HasString("summaryJsonPath", "/tmp/ucli/compile/run-1/summary.json")
            .HasString("diagnosticsJsonPath", "/tmp/ucli/compile/run-1/diagnostics.json");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DaemonStartProgressContracts_SerializeWithCamelCaseFields ()
    {
        var payload = new DaemonStartProgressEntry(
            ProjectFingerprint: "project-fingerprint",
            TimeoutMilliseconds: 10000,
            EditorMode: "batchmode",
            OnStartupBlocked: "auto",
            Result: ContractLiteralCodec.ToValue(CommandProgressResult.Failed),
            StartStatus: "failed",
            DaemonStatus: "notRunning",
            ErrorCode: "IPC_TIMEOUT");

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload, SerializerOptions));

        Assert.Equal("daemon.start.started", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started));
        Assert.Equal("daemon.start.pluginVerification.started", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationStarted));
        Assert.Equal("daemon.start.pluginVerification.completed", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationCompleted));
        Assert.Equal("daemon.start.supervisorBootstrap.started", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted));
        Assert.Equal("daemon.start.supervisorBootstrap.completed", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted));
        Assert.Equal("daemon.start.ensureRunning.started", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningStarted));
        Assert.Equal("daemon.start.ensureRunning.completed", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningCompleted));
        Assert.Equal("daemon.start.completed", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed));
        Assert.Equal("succeeded", ContractLiteralCodec.ToValue(CommandProgressResult.Succeeded));
        Assert.Equal("failed", ContractLiteralCodec.ToValue(CommandProgressResult.Failed));
        JsonAssert.For(document.RootElement)
            .HasString("projectFingerprint", "project-fingerprint")
            .HasInt32("timeoutMilliseconds", 10000)
            .HasString("editorMode", "batchmode")
            .HasString("onStartupBlocked", "auto")
            .HasString("result", "failed")
            .HasString("startStatus", "failed")
            .HasString("daemonStatus", "notRunning")
            .HasString("errorCode", "IPC_TIMEOUT");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcDaemonLogsReadRequest(
            Tail: 120,
            After: "stream-1:10",
            Since: "2026-03-05T10:30:00+09:00",
            Until: "2026-03-05T10:40:00+09:00",
            Level: "warning",
            Query: "connection",
            QueryTarget: "message",
            Category: "transport");
        var responsePayload = new IpcDaemonLogsReadResponse(
            Events:
            [
                new IpcDaemonLogEvent(
                    Timestamp: "2026-03-05T10:35:22.0000000+09:00",
                    Level: "warning",
                    Category: "transport",
                    Message: "Named pipe listener ignored recoverable connection error.",
                    Raw: "IOException: broken pipe",
                    Cursor: "stream-1:42"),
            ],
            NextCursor: "stream-1:43");

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasInt32("tail", 120)
            .HasString("after", "stream-1:10")
            .HasString("since", "2026-03-05T10:30:00+09:00")
            .HasString("until", "2026-03-05T10:40:00+09:00")
            .HasString("level", "warning")
            .HasString("query", "connection")
            .HasString("queryTarget", "message")
            .HasString("category", "transport");
        JsonAssert.For(responseDocument.RootElement)
            .HasArrayLength("events", 1)
            .HasString("nextCursor", "stream-1:43")
            .HasProperty("events", 0, eventObject => eventObject
                .HasString("timestamp", "2026-03-05T10:35:22.0000000+09:00")
                .HasString("level", "warning")
                .HasString("category", "transport")
                .HasString("message", "Named pipe listener ignored recoverable connection error.")
                .HasString("raw", "IOException: broken pipe")
                .HasString("cursor", "stream-1:42"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcUnityLogsReadRequest(
            Tail: 50,
            After: "stream-1:10",
            Since: "2026-03-05T10:30:00+09:00",
            Until: "2026-03-05T10:40:00+09:00",
            Level: "warning",
            Query: "socket",
            QueryTarget: "stack",
            Source: "runtime",
            StackTrace: "error",
            StackTraceMaxFrames: 10,
            StackTraceMaxChars: 2048);
        var responsePayload = new IpcUnityLogsReadResponse(
            Events:
            [
                new IpcUnityLogEvent(
                    Timestamp: "2026-03-05T10:35:22.0000000+09:00",
                    Level: "warning",
                    Source: "runtime",
                    Message: "Socket timeout detected.",
                    StackTrace: "at Listener.Run()",
                    Cursor: "stream-1:42"),
            ],
            NextCursor: "stream-1:43");

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasInt32("tail", 50)
            .HasString("after", "stream-1:10")
            .HasString("since", "2026-03-05T10:30:00+09:00")
            .HasString("until", "2026-03-05T10:40:00+09:00")
            .HasString("level", "warning")
            .HasString("query", "socket")
            .HasString("queryTarget", "stack")
            .HasString("source", "runtime")
            .HasString("stackTrace", "error")
            .HasInt32("stackTraceMaxFrames", 10)
            .HasInt32("stackTraceMaxChars", 2048);
        JsonAssert.For(responseDocument.RootElement)
            .HasArrayLength("events", 1)
            .HasString("nextCursor", "stream-1:43")
            .HasProperty("events", 0, eventObject => eventObject
                .HasString("timestamp", "2026-03-05T10:35:22.0000000+09:00")
                .HasString("level", "warning")
                .HasString("source", "runtime")
                .HasString("message", "Socket timeout detected.")
                .HasString("stackTrace", "at Listener.Run()")
                .HasString("cursor", "stream-1:42"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityConsoleClearContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcUnityConsoleClearRequest(UcliCommandIds.LogsUnityClear.Name);
        var responsePayload = new IpcUnityConsoleClearResponse();

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        JsonAssert.For(requestDocument.RootElement)
            .HasString("requestedBy", UcliCommandIds.LogsUnityClear.Name);
        JsonAssert.For(responseDocument.RootElement)
            .HasValueKind(JsonValueKind.Object);
        Assert.Empty(responseDocument.RootElement.EnumerateObject());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesWithOpResultsContract ()
    {
        var response = new IpcExecuteResponse(new[]
        {
            new IpcExecuteOperationResult(
                OpId: "op-1",
                Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: new[]
                {
                    new IpcExecuteTouchedResource(
                        Kind: UcliTouchedResourceKindNames.Scene,
                        Path: "Assets/Scenes/Main.unity",
                        Guid: "11111111111111111111111111111111"),
                }),
        })
        {
            PlanToken = "issued-token",
            Project = new IpcProjectIdentity(
                ProjectPath: "/repo/UnityProject",
                ProjectFingerprint: "project-fingerprint",
                UnityVersion: "6000.1.4f1"),
        };

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasArrayLength("opResults", 1)
            .HasProperty("project", project => project
                .HasString("projectPath", "/repo/UnityProject")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasString("unityVersion", "6000.1.4f1"))
            .HasString("planToken", "issued-token")
            .HasProperty("opResults", 0, opResult => opResult
                .HasString("opId", "op-1")
                .HasString("op", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve)
                .HasString("phase", IpcExecuteOperationPhaseNames.Call)
                .HasBoolean("applied", true)
                .HasBoolean("changed", true)
                .HasArrayLength("touched", 1)
                .HasArrayLength("diagnostics", 0)
                .HasProperty("touched", 0, touched => touched
                    .HasString("kind", UcliTouchedResourceKindNames.Scene)
                    .HasString("path", "Assets/Scenes/Main.unity")
                    .HasString("guid", "11111111111111111111111111111111")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_RoundTripsContractViolations ()
    {
        var response = new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())
        {
            ContractViolations =
            [
                new IpcExecuteContractViolation(
                    OpId: "step-1",
                    Operation: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                    ExpectedFact: "assurance.mayDirty=false",
                    ObservedResult: "opResults[].changed=true",
                    ApplicationState: IpcExecuteApplicationStateNames.Indeterminate),
            ],
        };

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        using var jsonDocument = JsonDocument.Parse(json);
        JsonAssert.For(jsonDocument.RootElement)
            .HasArrayLength("contractViolations", 1)
            .HasProperty("contractViolations", 0, violation => violation
                .HasString("opId", "step-1")
                .HasString("operation", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh)
                .HasString("expectedFact", "assurance.mayDirty=false")
                .HasString("observedResult", "opResults[].changed=true")
                .HasString("applicationState", IpcExecuteApplicationStateNames.Indeterminate));

        var roundTrip = JsonSerializer.Deserialize<IpcExecuteResponse>(json, SerializerOptions);

        Assert.NotNull(roundTrip);
        var violationResult = Assert.Single(roundTrip.ContractViolations!);
        Assert.Equal("step-1", violationResult.OpId);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh, violationResult.Operation);
        Assert.Equal("assurance.mayDirty=false", violationResult.ExpectedFact);
        Assert.Equal("opResults[].changed=true", violationResult.ObservedResult);
        Assert.Equal(IpcExecuteApplicationStateNames.Indeterminate, violationResult.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteOperationResultFactory_CreatePlanResult_CreatesSharedEnvelopeContract ()
    {
        var payload = JsonSerializer.SerializeToElement(
            new IpcResolveOperationResult("GlobalObjectId_V1-2-3-4-5-6"),
            SerializerOptions);
        var opResult = IpcExecuteOperationResultFactory.CreatePlanResult(
            opId: "resolve",
            op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
            applied: false,
            changed: false,
            touched: Array.Empty<IpcExecuteTouchedResource>(),
            result: payload,
            diagnostics:
            [
                new IpcExecuteDiagnostic(
                    Code: ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                    Severity: IpcExecuteDiagnosticSeverityNames.Warning,
                    CoverageImpact: IpcExecuteDiagnosticCoverageImpactNames.Partial,
                    Message: "Scene query skipped GameObjects whose names contain '/'."),
            ]);

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(opResult, SerializerOptions));

        JsonAssert.For(jsonDocument.RootElement)
            .HasString("opId", "resolve")
            .HasString("op", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve)
            .HasString("phase", IpcExecuteOperationPhaseNames.Plan)
            .HasBoolean("applied", false)
            .HasBoolean("changed", false)
            .HasArrayLength("touched", 0)
            .HasArrayLength("diagnostics", 1)
            .HasProperty("diagnostics", 0, diagnostic => diagnostic
                .HasString("code", "HIERARCHY_PATH_UNREPRESENTABLE_OBJECTS")
                .HasString("severity", IpcExecuteDiagnosticSeverityNames.Warning)
                .HasString("coverageImpact", IpcExecuteDiagnosticCoverageImpactNames.Partial)
                .HasString("message", "Scene query skipped GameObjects whose names contain '/'."))
            .HasProperty("result", result => result
                .HasString("globalObjectId", "GlobalObjectId_V1-2-3-4-5-6"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResolveOperationResult_SerializesWithCamelCaseContractFields ()
    {
        var payload = new IpcResolveOperationResult("GlobalObjectId_V1-2-3-4-5-6");

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(payload, SerializerOptions));

        JsonAssert.For(jsonDocument.RootElement)
            .HasString("globalObjectId", "GlobalObjectId_V1-2-3-4-5-6");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResolveSelectorArgsSchema_UsesCanonicalResolveSelectorPropertyNames ()
    {
        using var jsonDocument = JsonDocument.Parse(IpcResolveSelectorArgsSchema.Json);

        var properties = jsonDocument.RootElement.GetProperty("properties");
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.GlobalObjectId, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.AssetGuid, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.AssetPath, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.ProjectAssetPath, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.Scene, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.Prefab, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.HierarchyPath, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.ComponentType, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesReadPostconditionContract ()
    {
        var response = new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurfaceNames.AssetSearch,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00")),
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"))
                {
                    ScenePath = "Assets/Scenes/Main.unity",
                },
            ]),
        };

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasProperty("readPostcondition", readPostcondition => readPostcondition
                .HasArrayLength("requirements", 2)
                .HasProperty("requirements", 0, requirement => requirement
                    .HasString("surface", IpcExecuteReadPostconditionSurfaceNames.AssetSearch)
                    .HasString("minSafeGeneratedAtUtc", "2026-04-23T00:00:00+00:00"))
                .HasProperty("requirements", 1, requirement => requirement
                    .HasString("surface", IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite)
                    .HasString("scenePath", "Assets/Scenes/Main.unity")
                    .HasString("minSafeGeneratedAtUtc", "2026-04-23T00:00:00+00:00")));
        Assert.False(jsonDocument.RootElement.GetProperty("readPostcondition").GetProperty("requirements")[0].TryGetProperty("scenePath", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesPostReadSourceContract ()
    {
        var response = new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())
        {
            PostReadSource = new IpcExecutePostReadSource(
                IpcExecutePostReadSource.CurrentSchemaVersion,
                [
                    new IpcExecutePostReadSourceStep(
                        OpId: "edit-1",
                        SourceKind: IpcExecutePostReadSourceKindNames.Edit,
                        PlayModeMutation: false,
                        Commit: IpcExecutePostReadCommitNames.Context,
                        PersistenceExpected: true,
                        ExpectedPostState: IpcExecuteExpectedPostStateNames.Deterministic),
                    new IpcExecutePostReadSourceStep(
                        OpId: "op-1",
                        SourceKind: IpcExecutePostReadSourceKindNames.Operation,
                        PlayModeMutation: false,
                        Commit: null,
                        PersistenceExpected: false,
                        ExpectedPostState: IpcExecuteExpectedPostStateNames.Unavailable),
                ]),
        };

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasProperty("postReadSource", postReadSource => postReadSource
                .HasInt32("schemaVersion", 1)
                .HasArrayLength("steps", 2)
                .HasProperty("steps", 0, step => step
                    .HasString("opId", "edit-1")
                    .HasString("sourceKind", IpcExecutePostReadSourceKindNames.Edit)
                    .HasBoolean("playModeMutation", false)
                    .HasString("commit", IpcExecutePostReadCommitNames.Context)
                    .HasBoolean("persistenceExpected", true)
                    .HasString("expectedPostState", IpcExecuteExpectedPostStateNames.Deterministic))
                .HasProperty("steps", 1, step => step
                    .HasString("opId", "op-1")
                    .HasString("sourceKind", IpcExecutePostReadSourceKindNames.Operation)
                    .IsNull("commit")
                    .HasBoolean("persistenceExpected", false)
                    .HasString("expectedPostState", IpcExecuteExpectedPostStateNames.Unavailable)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesContractViolationsContract ()
    {
        var response = new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())
        {
            ContractViolations =
            [
                new IpcExecuteContractViolation(
                    OpId: "query-1",
                    Operation: UcliPrimitiveOperationNames.SceneQuery,
                    ExpectedFact: "operation.kind=query",
                    ObservedResult: "opResults[].applied=true",
                    ApplicationState: IpcExecuteApplicationStateNames.Applied),
            ],
        };

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasArrayLength("contractViolations", 1)
            .HasProperty("contractViolations", 0, violation => violation
                .HasString("opId", "query-1")
                .HasString("operation", UcliPrimitiveOperationNames.SceneQuery)
                .HasString("expectedFact", "operation.kind=query")
                .HasString("observedResult", "opResults[].applied=true")
                .HasString("applicationState", IpcExecuteApplicationStateNames.Applied));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_OmitsPlanTokenWhenNull ()
    {
        var response = new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>());

        var jsonElement = JsonSerializer.SerializeToElement(response, SerializerOptions);
        Assert.True(jsonElement.TryGetProperty("project", out _));
        Assert.False(jsonElement.TryGetProperty("planToken", out _));
        Assert.False(jsonElement.TryGetProperty("readPostcondition", out _));
        Assert.False(jsonElement.TryGetProperty("postReadSource", out _));
        Assert.False(jsonElement.TryGetProperty("contractViolations", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteReadPostconditionSurfaceNames_ExposeExpectedLiterals ()
    {
        Assert.Equal("assetSearch", IpcExecuteReadPostconditionSurfaceNames.AssetSearch);
        Assert.Equal("guidPath", IpcExecuteReadPostconditionSurfaceNames.GuidPath);
        Assert.Equal("sceneTreeLite", IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteApplicationStateNames_ExposeExpectedLiterals ()
    {
        Assert.Equal("notApplied", IpcExecuteApplicationStateNames.NotApplied);
        Assert.Equal("applied", IpcExecuteApplicationStateNames.Applied);
        Assert.Equal("indeterminate", IpcExecuteApplicationStateNames.Indeterminate);
        Assert.Equal("unknown", IpcExecuteApplicationStateNames.Unknown);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteCommandNames_ExposeExpectedCommandLiterals ()
    {
        Assert.Equal("validate", UcliCommandIds.Validate.Name);
        Assert.Equal("plan", UcliCommandIds.Plan.Name);
        Assert.Equal("call", UcliCommandIds.Call.Name);
        Assert.Equal("resolve", UcliCommandIds.Resolve.Name);
        Assert.Equal("query", UcliCommandIds.Query.Name);
        Assert.Equal("refresh", UcliCommandIds.Refresh.Name);
        Assert.Equal("ready", UcliCommandIds.Ready.Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliCommandIds_ExposeLogsCommandLiterals ()
    {
        Assert.Equal("logs", UcliCommandIds.Logs.Name);
        Assert.Equal("logs.daemon.read", UcliCommandIds.LogsDaemonRead.Name);
        Assert.Equal("logs.unity.read", UcliCommandIds.LogsUnityRead.Name);
        Assert.Equal("logs.unity.clear", UcliCommandIds.LogsUnityClear.Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliCommandIds_ExposeCodesCommandLiterals ()
    {
        Assert.Equal("codes", UcliCommandIds.Codes.Name);
        Assert.Equal("codes.list", UcliCommandIds.CodesList.Name);
        Assert.Equal("codes.describe", UcliCommandIds.CodesDescribe.Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliCommandIds_ExposeDaemonCommandLiterals ()
    {
        Assert.Equal("daemon", UcliCommandIds.Daemon.Name);
        Assert.Equal("daemon.start", UcliCommandIds.DaemonStart.Name);
        Assert.Equal("daemon.stop", UcliCommandIds.DaemonStop.Name);
        Assert.Equal("daemon.cleanup", UcliCommandIds.DaemonCleanup.Name);
        Assert.Equal("daemon.status", UcliCommandIds.DaemonStatus.Name);
        Assert.Equal("daemon.list", UcliCommandIds.DaemonList.Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteCommandNames_ClassifiesKnownAndOperationPipelineCommands ()
    {
        Assert.True(IpcExecuteCommandNames.IsKnown(UcliCommandIds.Validate.Name));
        Assert.True(IpcExecuteCommandNames.IsKnown(UcliCommandIds.Plan.Name));
        Assert.True(IpcExecuteCommandNames.IsKnown(UcliCommandIds.Call.Name));
        Assert.True(IpcExecuteCommandNames.IsKnown(UcliCommandIds.Resolve.Name));
        Assert.True(IpcExecuteCommandNames.IsKnown(UcliCommandIds.Query.Name));
        Assert.False(IpcExecuteCommandNames.IsKnown(UcliCommandIds.Refresh.Name));
        Assert.False(IpcExecuteCommandNames.IsKnown(UcliCommandIds.Ready.Name));
        Assert.False(IpcExecuteCommandNames.IsKnown("unknown"));

        Assert.False(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Validate.Name));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Plan.Name));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Call.Name));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Resolve.Name));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Query.Name));
        Assert.False(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Refresh.Name));
        Assert.False(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Ready.Name));
        Assert.False(IpcExecuteCommandNames.IsOperationPipelineCommand("unknown"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteOperationPhaseNames_ExposeExpectedLiterals ()
    {
        Assert.Equal("validate", IpcExecuteOperationPhaseNames.Validate);
        Assert.Equal("plan", IpcExecuteOperationPhaseNames.Plan);
        Assert.Equal("call", IpcExecuteOperationPhaseNames.Call);
        Assert.Equal("skipped", IpcExecuteOperationPhaseNames.Skipped);
    }

    private static UcliOperationDescribeContract CreateGoDescribeContract ()
    {
        return UcliOperationDescribeContractBuilder.Create<GoDescribeArgs, GameObjectDescriptionResult>(
            "Returns a GameObject description including components and child hierarchy.",
            new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()));
    }

    private static IpcBuildLifecycleSnapshot CreateBuildLifecycleSnapshot (
        string generationSuffix,
        bool canAcceptExecutionRequests)
    {
        return new IpcBuildLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            LifecycleState: "ready",
            BlockingReason: "none",
            CompileState: "idle",
            CompileGeneration: $"compile-{generationSuffix}",
            DomainReloadGeneration: $"domain-{generationSuffix}",
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new IpcPlayModeSnapshot(
                State: "stopped",
                Transition: "none",
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: $"play-{generationSuffix}"),
            AssetRefreshGeneration: $"asset-{generationSuffix}");
    }

    private static IpcPlayLifecycleSnapshot CreatePlayLifecycleSnapshot (
        string playModeState,
        string transition)
    {
        return new IpcPlayLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: "gui",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            LifecycleState: "ready",
            BlockingReason: "none",
            CompileState: "idle",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new IpcPlayModeSnapshot(
                State: playModeState,
                Transition: transition,
                IsPlaying: string.Equals(playModeState, "playing", StringComparison.Ordinal),
                IsPlayingOrWillChangePlaymode: string.Equals(playModeState, "playing", StringComparison.Ordinal)
                    || string.Equals(transition, "entering", StringComparison.Ordinal),
                Generation: "42"));
    }

    private static IpcCompileSummary CreateCompileSummary ()
    {
        var primaryDiagnostic = new IpcPrimaryDiagnostic(
            Kind: "compiler",
            Code: "CS1002",
            File: "Assets/Broken.cs",
            Line: 4,
            Column: 16,
            Message: "; expected");
        return new IpcCompileSummary(
            RunId: "run-1",
            ProjectFingerprint: "project-fingerprint",
            Completed: true,
            StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00+00:00"),
            CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02+00:00"),
            Refresh: new IpcCompileSummary.RefreshEvidence(
                Origin: "assetDatabaseRefresh",
                Requested: true,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00+00:00"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:01+00:00"),
                Completed: true),
            ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: "12",
                CompileGenerationAfter: "14",
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                    ErrorCount: 1,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: "7",
                GenerationAfter: "7",
                Settled: true),
            Lifecycle: new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                EditorMode: "batchmode",
                LifecycleState: "compileFailed",
                BlockingReason: "compileFailed",
                CompileState: "failed",
                CompileGeneration: "14",
                DomainReloadGeneration: "7",
                CanAcceptExecutionRequests: false,
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02+00:00"),
                ActionRequired: "fixCompileErrors",
                PrimaryDiagnostic: primaryDiagnostic));
    }
}
