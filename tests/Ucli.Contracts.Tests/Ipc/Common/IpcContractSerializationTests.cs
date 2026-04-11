using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

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
            Payload: payload);

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(request, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("protocolVersion", 1)
            .HasString("requestId", "req-1")
            .HasString("sessionToken", "token")
            .HasString("method", "execute")
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
                new IpcError("COMMAND_NOT_IMPLEMENTED", "Not implemented", null),
            ]);

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        var roundTrip = JsonSerializer.Deserialize<IpcResponse>(json, SerializerOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(response.ProtocolVersion, roundTrip.ProtocolVersion);
        Assert.Equal(response.RequestId, roundTrip.RequestId);
        Assert.Equal(response.Status, roundTrip.Status);
        Assert.Single(roundTrip.Errors);
        Assert.Equal("COMMAND_NOT_IMPLEMENTED", roundTrip.Errors[0].Code);
        Assert.Equal("Not implemented", roundTrip.Errors[0].Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcErrorCodes_ExposeCoreAndReadIndexConstants ()
    {
        Assert.Equal("INVALID_ARGUMENT", IpcErrorCodes.InvalidArgument);
        Assert.Equal("NOT_INITIALIZED", IpcErrorCodes.NotInitialized);
        Assert.Equal("SESSION_TOKEN_REQUIRED", IpcErrorCodes.SessionTokenRequired);
        Assert.Equal("SESSION_TOKEN_INVALID", IpcErrorCodes.SessionTokenInvalid);
        Assert.Equal("READ_INDEX_BOOTSTRAP_FAILED", IpcErrorCodes.ReadIndexBootstrapFailed);
        Assert.Equal("READ_INDEX_FORMAT_INVALID", IpcErrorCodes.ReadIndexFormatInvalid);
        Assert.Equal("READ_INDEX_FRESH_REQUIRED", IpcErrorCodes.ReadIndexFreshRequired);
        Assert.Equal("PLAN_TOKEN_REQUIRED", IpcErrorCodes.PlanTokenRequired);
        Assert.Equal("PLAN_TOKEN_INVALID", IpcErrorCodes.PlanTokenInvalid);
        Assert.Equal("PLAN_TOKEN_EXPIRED", IpcErrorCodes.PlanTokenExpired);
        Assert.Equal("PLAN_TOKEN_REQUEST_MISMATCH", IpcErrorCodes.PlanTokenRequestMismatch);
        Assert.Equal("STATE_CHANGED_SINCE_PLAN", IpcErrorCodes.StateChangedSincePlan);
        Assert.Equal("REQUEST_ID_CONFLICT", IpcErrorCodes.RequestIdConflict);
        Assert.Equal("EDITOR_STARTING", IpcErrorCodes.EditorStarting);
        Assert.Equal("EDITOR_BUSY", IpcErrorCodes.EditorBusy);
        Assert.Equal("EDITOR_COMPILING", IpcErrorCodes.EditorCompiling);
        Assert.Equal("EDITOR_DOMAIN_RELOADING", IpcErrorCodes.EditorDomainReloading);
        Assert.Equal("EDITOR_PLAYMODE", IpcErrorCodes.EditorPlaymode);
        Assert.Equal("EDITOR_MODAL_BLOCKED", IpcErrorCodes.EditorModalBlocked);
        Assert.Equal("EDITOR_SAFE_MODE", IpcErrorCodes.EditorSafeMode);
        Assert.Equal("EDITOR_SHUTTING_DOWN", IpcErrorCodes.EditorShuttingDown);
        Assert.Equal("INTERNAL_ERROR", IpcErrorCodes.InternalError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteRequest_SerializesPlanTokenOnlyWhenSpecified ()
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
        };

        var withTokenJson = JsonSerializer.SerializeToElement(requestWithToken, SerializerOptions);
        Assert.True(withTokenJson.TryGetProperty("planToken", out var planTokenElement));
        Assert.Equal("token-value", planTokenElement.GetString());
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
        Assert.True(withoutTokenJson.TryGetProperty("failFast", out var failFastElement));
        Assert.True(failFastElement.GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcMethodNames_ExposeExpectedMethodLiterals ()
    {
        Assert.Equal("ping", IpcMethodNames.Ping);
        Assert.Equal("execute", IpcMethodNames.Execute);
        Assert.Equal("ops.read", IpcMethodNames.OpsRead);
        Assert.Equal("index.assets.read", IpcMethodNames.IndexAssetsRead);
        Assert.Equal("test.run", IpcMethodNames.TestRun);
        Assert.Equal("shutdown", IpcMethodNames.Shutdown);
        Assert.Equal("daemon.logs.read", IpcMethodNames.DaemonLogsRead);
        Assert.Equal("unity.logs.read", IpcMethodNames.UnityLogsRead);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcOpsReadContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcOpsReadRequest();
        var responsePayload = new IpcOpsReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            Operations:
            [
                new IndexOpEntryJsonContract(
                    Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}"""),
            ]);

        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(requestPayload, SerializerOptions));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(responsePayload, SerializerOptions));

        Assert.Equal(JsonValueKind.Object, requestDocument.RootElement.ValueKind);
        JsonAssert.For(responseDocument.RootElement)
            .HasString("generatedAtUtc", "2026-03-06T00:00:00+00:00")
            .HasArrayLength("operations", 1)
            .HasProperty("operations", 0, operation => operation
                .HasString("name", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe)
                .HasString("kind", "query")
                .HasString("policy", "safe")
                .HasString("argsSchemaJson", """{"type":"object"}"""));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcIndexAssetsReadContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcIndexAssetsReadRequest();
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

        Assert.Equal(JsonValueKind.Object, requestDocument.RootElement.ValueKind);
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
            BuildTarget: null,
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
            .IsNull("buildTarget")
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
                        Kind: IpcExecuteTouchedResourceKindNames.Scene,
                        Path: "Assets/Scenes/Main.unity",
                        Guid: "11111111111111111111111111111111"),
                }),
        })
        {
            PlanToken = "issued-token",
        };

        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        JsonAssert.For(jsonDocument.RootElement)
            .HasArrayLength("opResults", 1)
            .HasString("planToken", "issued-token")
            .HasProperty("opResults", 0, opResult => opResult
                .HasString("opId", "op-1")
                .HasString("op", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve)
                .HasString("phase", IpcExecuteOperationPhaseNames.Call)
                .HasBoolean("applied", true)
                .HasBoolean("changed", true)
                .HasArrayLength("touched", 1)
                .HasProperty("touched", 0, touched => touched
                    .HasString("kind", IpcExecuteTouchedResourceKindNames.Scene)
                    .HasString("path", "Assets/Scenes/Main.unity")
                    .HasString("guid", "11111111111111111111111111111111")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_OmitsPlanTokenWhenNull ()
    {
        var response = new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>());

        var jsonElement = JsonSerializer.SerializeToElement(response, SerializerOptions);
        Assert.False(jsonElement.TryGetProperty("planToken", out _));
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
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliCommandIds_ExposeLogsCommandLiterals ()
    {
        Assert.Equal("logs", UcliCommandIds.Logs.Name);
        Assert.Equal("logs.daemon", UcliCommandIds.LogsDaemon.Name);
        Assert.Equal("logs.unity", UcliCommandIds.LogsUnity.Name);
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
        Assert.False(IpcExecuteCommandNames.IsKnown("unknown"));

        Assert.False(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Validate.Name));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Plan.Name));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Call.Name));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Resolve.Name));
        Assert.True(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Query.Name));
        Assert.False(IpcExecuteCommandNames.IsOperationPipelineCommand(UcliCommandIds.Refresh.Name));
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

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteTouchedResourceKindNames_ExposeExpectedLiterals ()
    {
        Assert.Equal("scene", IpcExecuteTouchedResourceKindNames.Scene);
        Assert.Equal("prefab", IpcExecuteTouchedResourceKindNames.Prefab);
        Assert.Equal("asset", IpcExecuteTouchedResourceKindNames.Asset);
        Assert.Equal("projectSettings", IpcExecuteTouchedResourceKindNames.ProjectSettings);
    }

}