using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Protocol;

public sealed class IpcEvalContractSerializationTests
{
    private static readonly Sha256Digest SourceDigest = Sha256Digest.Parse(new string('a', 64));
    private static readonly Sha256Digest ExecutionDigest = Sha256Digest.Parse(new string('b', 64));
    private static readonly UnityProjectIdentity Project = new(
        "Project",
        new ProjectFingerprint(new string('c', 64)),
        "2023.2.22f1");

    [Fact]
    [Trait("Size", "Small")]
    public void EvalRequests_AreSeparateClosedContracts ()
    {
        var plan = IpcPayloadCodec.SerializeToElement(new IpcEvalPlanRequest(
            "return null;", CsEvalSourceKind.Snippet, allowDangerous: true, allowPlayMode: false));
        var call = IpcPayloadCodec.SerializeToElement(new IpcEvalCallRequest(
            "return null;", CsEvalSourceKind.Snippet, allowDangerous: true, allowPlayMode: false, "token"));

        Assert.False(plan.TryGetProperty("planToken", out _));
        Assert.Equal("token", call.GetProperty("planToken").GetString());
        Assert.False(IpcPayloadCodec.TryDeserializeStrict<IpcEvalPlanRequest>(AppendUnknown(plan), out _, out _));
        Assert.False(IpcPayloadCodec.TryDeserializeStrict<IpcEvalCallRequest>(plan, out _, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvalPlanResponse_UsesOnlyTheFivePropertyPlanResultAndRoundTrips ()
    {
        var response = new IpcEvalResponse(
            Project,
            CsEvalPhase.Plan,
            ExecutionApplicationState.NotApplied,
            CreatePlan(),
            "token",
            null);

        var json = IpcPayloadCodec.SerializeToElement(response);

        var eval = json.GetProperty("eval");
        Assert.Equal(5, eval.EnumerateObject().Count());
        Assert.False(eval.TryGetProperty("planToken", out _));
        Assert.True(IpcPayloadCodec.TryDeserializeStrict(json, out IpcEvalResponse read, out var error), error.Message);
        Assert.IsType<CsEvalPlanSuccessResult>(read.Eval);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvalCallResponse_UsesTheNinePropertyCallResultAndRoundTrips ()
    {
        var response = new IpcEvalResponse(
            Project,
            CsEvalPhase.Call,
            ExecutionApplicationState.Applied,
            CreateCall(),
            null,
            CreateCallReadPostcondition());

        var json = IpcPayloadCodec.SerializeToElement(response);

        Assert.Equal(9, json.GetProperty("eval").EnumerateObject().Count());
        Assert.False(json.TryGetProperty("planToken", out _));
        Assert.True(IpcPayloadCodec.TryDeserializeStrict(json, out IpcEvalResponse read, out var error), error.Message);
        Assert.IsType<CsEvalCallSuccessResult>(read.Eval);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvalResponse_RejectsUnknownTopLevelProperty ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new IpcEvalResponse(
            Project,
            CsEvalPhase.Plan,
            ExecutionApplicationState.NotApplied,
            CreatePlan(),
            "token",
            null));

        Assert.False(IpcPayloadCodec.TryDeserializeStrict<IpcEvalResponse>(AppendUnknown(json), out _, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvalErrorResponse_RoundTripsPartialEvidenceAndReadPostconditionStrictly ()
    {
        var response = new IpcEvalErrorResponse(
            Project,
            CsEvalPhase.Call,
            ExecutionApplicationState.Indeterminate,
            new CsEvalPartialErrorResult(
                SourceDigest,
                CsEvalSourceKind.Snippet,
                "Snippet.Run",
                ExecutionDigest,
                new CsEvalPlanCompileResult(succeeded: true, []),
                durationMilliseconds: 1,
                logs: [],
                returnValue: CsEvalReturnValue.Null(),
                touchedResources: new CsEvalTouchedResources(true, [], [], [], [])),
            new ExecutionReadPostcondition([]));

        var json = IpcPayloadCodec.SerializeToElement(response);

        Assert.True(IpcPayloadCodec.TryDeserializeStrict(json, out IpcEvalErrorResponse read, out var error), error.Message);
        Assert.NotNull(read.Eval);
        Assert.NotNull(read.ReadPostcondition);
        Assert.False(IpcPayloadCodec.TryDeserializeStrict<IpcEvalErrorResponse>(AppendUnknown(json), out _, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvalCallResults_RejectLogsThatAreNotContiguousOneBasedSequences ()
    {
        var invalidLogs = new[]
        {
            new CsEvalLogEntry(1, CsEvalLogLevel.Info, "first", null),
            new CsEvalLogEntry(3, CsEvalLogLevel.Info, "third", null),
        };

        Assert.Throws<ArgumentException>(() => CreateCall(invalidLogs));
        Assert.Throws<ArgumentException>(() => new CsEvalPartialErrorResult(
            SourceDigest,
            CsEvalSourceKind.Snippet,
            "Snippet.Run",
            ExecutionDigest,
            new CsEvalPlanCompileResult(succeeded: true, []),
            durationMilliseconds: 0,
            logs: invalidLogs,
            returnValue: null,
            touchedResources: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TouchedResources_NormalizesOrdinalDuplicatesAndEnforcesNoChanges ()
    {
        var touched = new CsEvalTouchedResources(
            noChanges: false,
            scenes: ["Assets/Z.unity", "Assets/A.unity", "Assets/Z.unity"],
            prefabs: ["Assets/P2.prefab", "Assets/P1.prefab"],
            assets: ["Assets/B.asset", "Assets/A.asset"],
            projectSettings: ["ProjectSettings/Z.asset", "ProjectSettings/A.asset"]);

        Assert.Equal(["Assets/A.unity", "Assets/Z.unity"], touched.Scenes);
        Assert.Equal(["Assets/P1.prefab", "Assets/P2.prefab"], touched.Prefabs);
        Assert.Equal(["Assets/A.asset", "Assets/B.asset"], touched.Assets);
        Assert.Equal(["ProjectSettings/A.asset", "ProjectSettings/Z.asset"], touched.ProjectSettings);
        Assert.Throws<ArgumentException>(() => new CsEvalTouchedResources(
            noChanges: true,
            scenes: ["Assets/A.unity"],
            prefabs: [],
            assets: [],
            projectSettings: []));
        Assert.Throws<ArgumentException>(() => new CsEvalTouchedResources(
            noChanges: false,
            scenes: [],
            prefabs: [],
            assets: [],
            projectSettings: []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvalCallResponse_RejectsAnEmptyOrIncompleteReadPostcondition ()
    {
        Assert.Throws<ArgumentException>(() => new IpcEvalResponse(
            Project,
            CsEvalPhase.Call,
            ExecutionApplicationState.Applied,
            CreateCall(),
            null,
            new ExecutionReadPostcondition([])));
        Assert.Throws<ArgumentException>(() => new IpcEvalResponse(
            Project,
            CsEvalPhase.Call,
            ExecutionApplicationState.Applied,
            CreateCall(),
            null,
            new ExecutionReadPostcondition(
            [
                new ExecutionReadPostconditionRequirement(
                    ExecutionReadPostconditionSurface.AssetSearch,
                    DateTimeOffset.UnixEpoch,
                    null),
                new ExecutionReadPostconditionRequirement(
                    ExecutionReadPostconditionSurface.GuidPath,
                    DateTimeOffset.UnixEpoch,
                    null),
            ])));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvalDiagnostics_AllowIndependentNullableSourceLocations ()
    {
        var lineOnly = new CsEvalDiagnostic(UcliDiagnosticSeverity.Error, "CS1000", "message", 1, null);
        var columnOnly = new CsEvalDiagnostic(UcliDiagnosticSeverity.Error, "CS1000", "message", null, 1);

        Assert.Equal(1, lineOnly.Line);
        Assert.Equal(1, columnOnly.Column);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsEvalDiagnostic(UcliDiagnosticSeverity.Error, "CS1000", "message", 0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsEvalDiagnostic(UcliDiagnosticSeverity.Error, "CS1000", "message", null, 0));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EvalReturnValue_PreservesJsonNullAndRejectsInvalidKindValueCombinations ()
    {
        var jsonNull = CsEvalReturnValue.Json(JsonDocument.Parse("null").RootElement.Clone());
        var serialized = IpcPayloadCodec.SerializeToElement(jsonNull);

        Assert.Equal(JsonValueKind.Null, serialized.GetProperty("value").ValueKind);
        Assert.True(IpcPayloadCodec.TryDeserializeStrict(serialized, out CsEvalReturnValue roundTripped, out var error), error.Message);
        Assert.Equal(JsonValueKind.Null, Assert.IsType<CsEvalJsonReturnValue>(roundTripped).Value.ValueKind);
        Assert.False(IpcPayloadCodec.TryDeserializeStrict<CsEvalReturnValue>(JsonDocument.Parse("{\"kind\":\"json\"}").RootElement, out _, out _));
        Assert.False(IpcPayloadCodec.TryDeserializeStrict<CsEvalReturnValue>(JsonDocument.Parse("{\"kind\":\"null\",\"value\":null}").RootElement, out _, out _));
    }

    private static CsEvalPlanSuccessResult CreatePlan () => new(
        SourceDigest,
        CsEvalSourceKind.Snippet,
        "Snippet.Run",
        ExecutionDigest,
        new CsEvalPlanCompileResult(succeeded: true, []));

    private static ExecutionReadPostcondition CreateCallReadPostcondition () => new(
    [
        new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.AssetSearch, DateTimeOffset.UnixEpoch, null),
        new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.GuidPath, DateTimeOffset.UnixEpoch, null),
        new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.SceneTreeLite, DateTimeOffset.UnixEpoch, null),
    ]);

    private static CsEvalCallSuccessResult CreateCall (IReadOnlyList<CsEvalLogEntry>? logs = null) => new(
        SourceDigest,
        CsEvalSourceKind.Snippet,
        "Snippet.Run",
        ExecutionDigest,
        new CsEvalPlanCompileResult(succeeded: true, []),
        durationMilliseconds: 1,
        logs: logs ?? [new CsEvalLogEntry(1, CsEvalLogLevel.Info, "done", null)],
        returnValue: CsEvalReturnValue.Null(),
        touchedResources: new CsEvalTouchedResources(
            noChanges: true,
            scenes: [],
            prefabs: [],
            assets: [],
            projectSettings: []));

    private static JsonElement AppendUnknown (JsonElement original)
    {
        using var document = JsonDocument.Parse(original.GetRawText()[..^1] + ",\"unknown\":true}");
        return document.RootElement.Clone();
    }
}
