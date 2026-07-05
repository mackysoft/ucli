using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcExecuteContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteRequest_SerializesOptionalExecutionControlsOnlyWhenSpecified ()
    {
        var requestWithToken = new IpcExecuteRequest(
            Command: UcliCommandIds.Call,
            Arguments: IpcPayloadCodec.SerializeToElement(new
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

        var withTokenJson = IpcPayloadCodec.SerializeToElement(requestWithToken);
        Assert.True(withTokenJson.TryGetProperty("planToken", out var planTokenElement));
        Assert.Equal("token-value", planTokenElement.GetString());
        Assert.True(withTokenJson.TryGetProperty("allowDangerous", out var allowDangerousElement));
        Assert.True(allowDangerousElement.GetBoolean());
        Assert.True(withTokenJson.TryGetProperty("allowPlayMode", out var allowPlayModeElement));
        Assert.True(allowPlayModeElement.GetBoolean());
        Assert.False(withTokenJson.TryGetProperty("failFast", out _));

        var requestWithoutToken = new IpcExecuteRequest(
            Command: UcliCommandIds.Plan,
            Arguments: IpcPayloadCodec.SerializeToElement(new
            {
                protocolVersion = 1,
                requestId = "req-1",
                steps = Array.Empty<object>(),
            }))
        {
            FailFast = true,
        };
        var withoutTokenJson = IpcPayloadCodec.SerializeToElement(requestWithoutToken);
        Assert.False(withoutTokenJson.TryGetProperty("planToken", out _));
        Assert.False(withoutTokenJson.TryGetProperty("allowDangerous", out _));
        Assert.False(withoutTokenJson.TryGetProperty("allowPlayMode", out _));
        Assert.True(withoutTokenJson.TryGetProperty("failFast", out var failFastElement));
        Assert.True(failFastElement.GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesWithOpResultsContract ()
    {
        var response = new IpcExecuteResponse(new[]
        {
            new IpcExecuteOperationResult(
                OpId: "op-1",
                Op: UcliPrimitiveOperationNames.Resolve,
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

        var json = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(json)
            .HasArrayLength("opResults", 1)
            .HasProperty("project", project => project
                .HasString("projectPath", "/repo/UnityProject")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasString("unityVersion", "6000.1.4f1"))
            .HasString("planToken", "issued-token")
            .HasProperty("opResults", 0, opResult => opResult
                .HasString("opId", "op-1")
                .HasString("op", UcliPrimitiveOperationNames.Resolve)
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
                    Operation: UcliPrimitiveOperationNames.ProjectRefresh,
                    ExpectedFact: "assurance.mayDirty=false",
                    ObservedResult: "opResults[].changed=true",
                    ApplicationState: IpcExecuteApplicationStateNames.Indeterminate),
            ],
        };

        var jsonElement = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(jsonElement)
            .HasArrayLength("contractViolations", 1)
            .HasProperty("contractViolations", 0, violation => violation
                .HasString("opId", "step-1")
                .HasString("operation", UcliPrimitiveOperationNames.ProjectRefresh)
                .HasString("expectedFact", "assurance.mayDirty=false")
                .HasString("observedResult", "opResults[].changed=true")
                .HasString("applicationState", IpcExecuteApplicationStateNames.Indeterminate));

        var roundTrip = JsonSerializer.Deserialize<IpcExecuteResponse>(
            jsonElement.GetRawText(),
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(roundTrip);
        var violationResult = Assert.Single(roundTrip.ContractViolations!);
        Assert.Equal("step-1", violationResult.OpId);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, violationResult.Operation);
        Assert.Equal("assurance.mayDirty=false", violationResult.ExpectedFact);
        Assert.Equal("opResults[].changed=true", violationResult.ObservedResult);
        Assert.Equal(IpcExecuteApplicationStateNames.Indeterminate, violationResult.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteOperationResultFactory_CreatePlanResult_CreatesSharedEnvelopeContract ()
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcResolveOperationResult("GlobalObjectId_V1-2-3-4-5-6"));
        var opResult = IpcExecuteOperationResultFactory.CreatePlanResult(
            opId: "resolve",
            op: UcliPrimitiveOperationNames.Resolve,
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

        var json = IpcPayloadCodec.SerializeToElement(opResult);

        JsonAssert.For(json)
            .HasString("opId", "resolve")
            .HasString("op", UcliPrimitiveOperationNames.Resolve)
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

        var json = IpcPayloadCodec.SerializeToElement(payload);

        JsonAssert.For(json)
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

        var json = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(json)
            .HasProperty("readPostcondition", readPostcondition => readPostcondition
                .HasArrayLength("requirements", 2)
                .HasProperty("requirements", 0, requirement => requirement
                    .HasString("surface", IpcExecuteReadPostconditionSurfaceNames.AssetSearch)
                    .HasString("minSafeGeneratedAtUtc", "2026-04-23T00:00:00+00:00"))
                .HasProperty("requirements", 1, requirement => requirement
                    .HasString("surface", IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite)
                    .HasString("scenePath", "Assets/Scenes/Main.unity")
                    .HasString("minSafeGeneratedAtUtc", "2026-04-23T00:00:00+00:00")));
        Assert.False(json.GetProperty("readPostcondition").GetProperty("requirements")[0].TryGetProperty("scenePath", out _));
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

        var json = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(json)
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

        var json = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(json)
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

        var jsonElement = IpcPayloadCodec.SerializeToElement(response);
        Assert.True(jsonElement.TryGetProperty("project", out _));
        Assert.False(jsonElement.TryGetProperty("planToken", out _));
        Assert.False(jsonElement.TryGetProperty("readPostcondition", out _));
        Assert.False(jsonElement.TryGetProperty("postReadSource", out _));
        Assert.False(jsonElement.TryGetProperty("contractViolations", out _));
    }
}
