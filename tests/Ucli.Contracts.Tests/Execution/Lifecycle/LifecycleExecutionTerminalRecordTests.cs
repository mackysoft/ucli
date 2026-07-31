using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Execution.Lifecycle;

public sealed class LifecycleExecutionTerminalRecordTests
{
    public static TheoryData<LifecycleExecutionKind> ExecutionKinds => new()
    {
        LifecycleExecutionKind.Refresh,
        LifecycleExecutionKind.Compile,
        LifecycleExecutionKind.PlayEnter,
        LifecycleExecutionKind.PlayExit,
    };

    [Theory]
    [MemberData(nameof(ExecutionKinds))]
    [Trait("Size", "Small")]
    public void ConcreteRecord_RoundTripsThroughClosedTaggedUnion (
        LifecycleExecutionKind kind)
    {
        var expected = CreateCompletedRecord(kind);

        var json = JsonSerializer.Serialize(
            expected,
            typeof(LifecycleExecutionTerminalRecord),
            IpcJsonSerializerOptions.StrictPropertyNames);
        var actual = JsonSerializer.Deserialize<LifecycleExecutionTerminalRecord>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames);

        Assert.Equal(expected, actual);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            TextVocabulary.GetText(kind),
            document.RootElement.GetProperty("executionKind").GetString());
        Assert.Equal(
            LifecycleExecutionContractTestFactory.ExecutionId,
            document.RootElement.GetProperty("executionId").GetGuid());
        Assert.Equal(
            "completed",
            document.RootElement.GetProperty("terminalReason").GetString());
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("result").ValueKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void NonCompletedRecord_WritesRequiredNullableFieldsAsNull ()
    {
        var record = new RefreshLifecycleExecutionTerminalRecord(
            LifecycleExecutionContractTestFactory.ExecutionId,
            DefinitionDigest(LifecycleExecutionKind.Refresh),
            LifecycleExecutionContractTestFactory.Project,
            LifecycleExecutionContractTestFactory.Host,
            LifecycleExecutionContractTestFactory.StartedGeneration,
            terminalGeneration: null,
            LifecycleExecutionContractTestFactory.DeadlineUtc,
            LifecycleExecutionContractTestFactory.StartedAtUtc,
            LifecycleExecutionContractTestFactory.DeadlineUtc,
            LifecycleExecutionTerminalReason.DeadlineExceeded,
            ExecutionApplicationState.Indeterminate,
            result: null,
            verdict: null,
            artifactRefs: Array.Empty<ArtifactRef>());

        var json = JsonSerializer.SerializeToElement(
            record,
            typeof(LifecycleExecutionTerminalRecord),
            IpcJsonSerializerOptions.StrictPropertyNames);

        Assert.Equal(JsonValueKind.Null, json.GetProperty("terminalGeneration").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("result").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("verdict").ValueKind);
    }

    [Theory]
    [MemberData(nameof(ExecutionKinds))]
    [Trait("Size", "Small")]
    public void UnityExitedRecord_RetainsOnlyConfirmedHostExitFacts (
        LifecycleExecutionKind kind)
    {
        var record = CreateUnityExitedRecord(kind);

        Assert.Equal(
            LifecycleExecutionTerminalReason.UnityExited,
            record.TerminalReason);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            record.ApplicationState);
        Assert.Null(record.TerminalGeneration);
        Assert.Null(record.Verdict);
        Assert.Empty(record.ArtifactRefs);
        Assert.Null(record switch
        {
            RefreshLifecycleExecutionTerminalRecord refresh => refresh.Result,
            CompileLifecycleExecutionTerminalRecord compile => compile.Result,
            PlayEnterLifecycleExecutionTerminalRecord playEnter =>
                playEnter.Result,
            PlayExitLifecycleExecutionTerminalRecord playExit =>
                playExit.Result,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        });

        var json = JsonSerializer.SerializeToElement(
            record,
            typeof(LifecycleExecutionTerminalRecord),
            IpcJsonSerializerOptions.StrictPropertyNames);
        Assert.Equal(
            JsonValueKind.Null,
            json.GetProperty("terminalGeneration").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            json.GetProperty("result").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            json.GetProperty("verdict").ValueKind);
        Assert.Empty(json.GetProperty("artifactRefs").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(ExecutionKinds))]
    [Trait("Size", "Small")]
    public void UnityExitedRecord_WhenTypedResultIsProvided_RejectsValue (
        LifecycleExecutionKind kind)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateUnityExitedRecord(kind, includeResult: true));

        Assert.Equal("result", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityExitedRecord_WhenUnconfirmedTerminalFactsAreProvided_RejectsValues ()
    {
        Assert.Equal(
            "terminalGeneration",
            Assert.Throws<ArgumentException>(() =>
                CreateUnityExitedRecord(
                    LifecycleExecutionKind.Refresh,
                    terminalGeneration:
                        LifecycleExecutionContractTestFactory.TerminalGeneration))
                .ParamName);
        Assert.Equal(
            "applicationState",
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CreateUnityExitedRecord(
                    LifecycleExecutionKind.Refresh,
                    applicationState: ExecutionApplicationState.Applied))
                .ParamName);
        Assert.Equal(
            "artifactRefs",
            Assert.Throws<ArgumentException>(() =>
                CreateUnityExitedRecord(
                    LifecycleExecutionKind.Refresh,
                    artifactRefs:
                    [
                        new PathArtifactRef(
                            new ArtifactKind("compile.diagnostics"),
                            new ArtifactMediaType("application/json"),
                            new ArtifactPath(
                                "lifecycle-executions/compile-diagnostics.json"),
                            Sha256Digest.Parse(new string('f', 64)),
                            sizeBytes: 128,
                            LifecycleExecutionContractTestFactory.StartedAtUtc
                                .AddSeconds(3)),
                    ]))
                .ParamName);
        Assert.Equal(
            "verdict",
            Assert.Throws<ArgumentException>(() =>
                CreateUnityExitedRecord(
                    LifecycleExecutionKind.Compile,
                    verdict: Verdict.Pass))
                .ParamName);
    }

    [Theory]
    [InlineData(LifecycleExecutionTerminalReason.DeadlineExceeded, -1)]
    [InlineData(LifecycleExecutionTerminalReason.Completed, 0)]
    [InlineData(LifecycleExecutionTerminalReason.Completed, 1)]
    [InlineData(LifecycleExecutionTerminalReason.ActionFailed, 0)]
    [InlineData(LifecycleExecutionTerminalReason.ProjectMismatch, 0)]
    [InlineData(LifecycleExecutionTerminalReason.HostMismatch, 0)]
    [InlineData(LifecycleExecutionTerminalReason.GenerationMismatch, 0)]
    [InlineData(LifecycleExecutionTerminalReason.UnityExited, 0)]
    [Trait("Size", "Small")]
    public void TerminalReason_WhenCompletionViolatesItsDeadlineBoundary_RejectsValue (
        LifecycleExecutionTerminalReason terminalReason,
        int secondsFromDeadline)
    {
        var isCompleted = terminalReason == LifecycleExecutionTerminalReason.Completed;
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RefreshLifecycleExecutionTerminalRecord(
                LifecycleExecutionContractTestFactory.ExecutionId,
                DefinitionDigest(LifecycleExecutionKind.Refresh),
                LifecycleExecutionContractTestFactory.Project,
                LifecycleExecutionContractTestFactory.Host,
                LifecycleExecutionContractTestFactory.StartedGeneration,
                isCompleted
                    ? LifecycleExecutionContractTestFactory.TerminalGeneration
                    : null,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc,
                LifecycleExecutionContractTestFactory.DeadlineUtc.AddSeconds(secondsFromDeadline),
                terminalReason,
                isCompleted
                    ? ExecutionApplicationState.Applied
                    : ExecutionApplicationState.Indeterminate,
                isCompleted
                    ? LifecycleExecutionContractTestFactory.CreateRefreshResult()
                    : null,
                verdict: null,
                artifactRefs: Array.Empty<ArtifactRef>()));

        Assert.Equal("completedAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompletedRecord_WhenResultOrTerminalGenerationIsMissing_RejectsValue ()
    {
        Assert.Equal(
            "result",
            Assert.Throws<ArgumentNullException>(() =>
                new RefreshLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(LifecycleExecutionKind.Refresh),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    LifecycleExecutionContractTestFactory.TerminalGeneration,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                    LifecycleExecutionTerminalReason.Completed,
                    ExecutionApplicationState.Applied,
                    result: null,
                    verdict: null,
                    artifactRefs: Array.Empty<ArtifactRef>())).ParamName);
        Assert.Equal(
            "TerminalGeneration",
            Assert.Throws<ArgumentNullException>(() =>
                new RefreshLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(LifecycleExecutionKind.Refresh),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    terminalGeneration: null,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                    LifecycleExecutionTerminalReason.Completed,
                    ExecutionApplicationState.Applied,
                    LifecycleExecutionContractTestFactory.CreateRefreshResult(),
                    verdict: null,
                    artifactRefs: Array.Empty<ArtifactRef>())).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ActionSpecificPlayResult_WhenProviderResultBelongsToAnotherAction_RejectsValue ()
    {
        var exitResult = LifecycleExecutionContractTestFactory.CreatePlayResult(
            PlayLifecycleTransitionCommand.Exit);

        var exception = Assert.Throws<ArgumentException>(() =>
            PlayEnterLifecycleTransitionResult.FromProviderResult(exitResult));

        Assert.Equal("Transition", exception.ParamName);
    }

    [Theory]
    [InlineData(LifecycleExecutionTerminalReason.Completed, false)]
    [InlineData(LifecycleExecutionTerminalReason.ActionFailed, true)]
    [Trait("Size", "Small")]
    public void PlayRecord_WhenTerminalReasonDisagreesWithTransitionOutcome_RejectsValue (
        LifecycleExecutionTerminalReason terminalReason,
        bool successfulResult)
    {
        var result = CreatePlayEnterResult(successfulResult);
        var exception = Assert.Throws<ArgumentException>(() =>
            new PlayEnterLifecycleExecutionTerminalRecord(
                LifecycleExecutionContractTestFactory.ExecutionId,
                DefinitionDigest(LifecycleExecutionKind.PlayEnter),
                LifecycleExecutionContractTestFactory.Project,
                LifecycleExecutionContractTestFactory.Host,
                LifecycleExecutionContractTestFactory.StartedGeneration,
                successfulResult
                    ? LifecycleExecutionContractTestFactory.TerminalGeneration
                    : LifecycleExecutionContractTestFactory.StartedGeneration,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                terminalReason,
                successfulResult
                    ? ExecutionApplicationState.Applied
                    : ExecutionApplicationState.NotApplied,
                result,
                verdict: null,
                artifactRefs: Array.Empty<ArtifactRef>()));

        Assert.Equal("result", exception.ParamName);
    }

    [Theory]
    [InlineData(LifecycleExecutionKind.PlayEnter)]
    [InlineData(LifecycleExecutionKind.PlayExit)]
    [Trait("Size", "Small")]
    public void PlayRecord_WhenDeadlineWinsAfterSuccessfulResult_PreservesResultAndGeneration (
        LifecycleExecutionKind kind)
    {
        var record = CreatePlayRecord(
            kind,
            LifecycleExecutionTerminalReason.DeadlineExceeded,
            ExecutionApplicationState.Applied,
            successfulResult: true);

        Assert.Equal(
            LifecycleExecutionTerminalReason.DeadlineExceeded,
            record.TerminalReason);
        Assert.Equal(
            LifecycleExecutionContractTestFactory.TerminalGeneration,
            record.TerminalGeneration);
        Assert.NotNull(record switch
        {
            PlayEnterLifecycleExecutionTerminalRecord enter => enter.Result,
            PlayExitLifecycleExecutionTerminalRecord exit => exit.Result,
            _ => null,
        });
    }

    [Theory]
    [InlineData(LifecycleExecutionKind.PlayEnter)]
    [InlineData(LifecycleExecutionKind.PlayExit)]
    [Trait("Size", "Small")]
    public void AppliedPlayTransition_WhenTopLevelApplicationStateIsNotApplied_RejectsValue (
        LifecycleExecutionKind kind)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlayRecord(
                kind,
                LifecycleExecutionTerminalReason.Completed,
                ExecutionApplicationState.NotApplied,
                successfulResult: true));

        Assert.Equal("ApplicationState", exception.ParamName);
    }

    [Theory]
    [InlineData(LifecycleExecutionKind.PlayEnter)]
    [InlineData(LifecycleExecutionKind.PlayExit)]
    [Trait("Size", "Small")]
    public void AlreadySatisfiedPlayTransition_RequiresNotAppliedApplicationState (
        LifecycleExecutionKind kind)
    {
        var record = CreateAlreadySatisfiedPlayRecord(
            kind,
            ExecutionApplicationState.NotApplied);

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateAlreadySatisfiedPlayRecord(
                kind,
                ExecutionApplicationState.Applied));

        Assert.Equal(ExecutionApplicationState.NotApplied, record.ApplicationState);
        Assert.Equal("ApplicationState", exception.ParamName);
    }

    [Theory]
    [InlineData(LifecycleExecutionKind.PlayEnter)]
    [InlineData(LifecycleExecutionKind.PlayExit)]
    [Trait("Size", "Small")]
    public void FailedPlayResult_WhenTopLevelApplicationStateDisagrees_RejectsValue (
        LifecycleExecutionKind kind)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlayRecord(
                kind,
                LifecycleExecutionTerminalReason.ActionFailed,
                ExecutionApplicationState.Indeterminate,
                successfulResult: false));

        Assert.Equal("result", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ActionWithoutVerdict_WhenVerdictIsProvided_RejectsValue ()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new RefreshLifecycleExecutionTerminalRecord(
                LifecycleExecutionContractTestFactory.ExecutionId,
                DefinitionDigest(LifecycleExecutionKind.Refresh),
                LifecycleExecutionContractTestFactory.Project,
                LifecycleExecutionContractTestFactory.Host,
                LifecycleExecutionContractTestFactory.StartedGeneration,
                LifecycleExecutionContractTestFactory.TerminalGeneration,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                LifecycleExecutionTerminalReason.Completed,
                ExecutionApplicationState.Applied,
                LifecycleExecutionContractTestFactory.CreateRefreshResult(),
                Verdict.Pass,
                Array.Empty<ArtifactRef>()));

        Assert.Equal("Verdict", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileRecord_WhenExecutionDidNotComplete_RejectsSyntheticVerdict ()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new CompileLifecycleExecutionTerminalRecord(
                LifecycleExecutionContractTestFactory.ExecutionId,
                DefinitionDigest(LifecycleExecutionKind.Compile),
                LifecycleExecutionContractTestFactory.Project,
                LifecycleExecutionContractTestFactory.Host,
                LifecycleExecutionContractTestFactory.StartedGeneration,
                terminalGeneration: null,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionTerminalReason.DeadlineExceeded,
                ExecutionApplicationState.Indeterminate,
                result: null,
                Verdict.Incomplete,
                Array.Empty<ArtifactRef>()));

        Assert.Equal("Verdict", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileRecord_WhenVerdictDoesNotMatchTypedEvidence_RejectsMismatch ()
    {
        var failedResult = LifecycleExecutionContractTestFactory.CreateCompileResult(errorCount: 1);
        var passedResult = LifecycleExecutionContractTestFactory.CreateCompileResult();

        var passException = Assert.Throws<ArgumentException>(() =>
            new CompileLifecycleExecutionTerminalRecord(
                LifecycleExecutionContractTestFactory.ExecutionId,
                DefinitionDigest(LifecycleExecutionKind.Compile),
                LifecycleExecutionContractTestFactory.Project,
                LifecycleExecutionContractTestFactory.Host,
                LifecycleExecutionContractTestFactory.StartedGeneration,
                LifecycleExecutionContractTestFactory.TerminalGeneration,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                LifecycleExecutionTerminalReason.Completed,
                ExecutionApplicationState.Applied,
                failedResult,
                Verdict.Pass,
                Array.Empty<ArtifactRef>()));
        var failException = Assert.Throws<ArgumentException>(() =>
            new CompileLifecycleExecutionTerminalRecord(
                LifecycleExecutionContractTestFactory.ExecutionId,
                DefinitionDigest(LifecycleExecutionKind.Compile),
                LifecycleExecutionContractTestFactory.Project,
                LifecycleExecutionContractTestFactory.Host,
                LifecycleExecutionContractTestFactory.StartedGeneration,
                LifecycleExecutionContractTestFactory.TerminalGeneration,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                LifecycleExecutionTerminalReason.Completed,
                ExecutionApplicationState.Applied,
                passedResult,
                Verdict.Fail,
                Array.Empty<ArtifactRef>()));

        Assert.Equal("verdict", passException.ParamName);
        Assert.Equal("verdict", failException.ParamName);
    }

    [Theory]
    [MemberData(nameof(ExecutionKinds))]
    [Trait("Size", "Small")]
    public void TypedResult_WhenFinalGenerationIsKnown_RequiresExactTerminalGeneration (
        LifecycleExecutionKind kind)
    {
        var mismatchedGeneration = new UnityEditorGenerationSnapshot(
            CompileGeneration: 12,
            DomainReloadGeneration: 21,
            AssetRefreshGeneration: 31,
            PlayModeGeneration: 41);

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateCompletedRecord(kind, mismatchedGeneration));

        Assert.Equal("terminalGeneration", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FailedPlayResult_WhenObservedGenerationIsKnown_RequiresExactTerminalGeneration ()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new PlayExitLifecycleExecutionTerminalRecord(
                LifecycleExecutionContractTestFactory.ExecutionId,
                DefinitionDigest(LifecycleExecutionKind.PlayExit),
                LifecycleExecutionContractTestFactory.Project,
                LifecycleExecutionContractTestFactory.Host,
                LifecycleExecutionContractTestFactory.StartedGeneration,
                LifecycleExecutionContractTestFactory.TerminalGeneration,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                LifecycleExecutionTerminalReason.ActionFailed,
                ExecutionApplicationState.NotApplied,
                CreatePlayExitResult(successful: false),
                verdict: null,
                artifactRefs: Array.Empty<ArtifactRef>()));

        Assert.Equal("terminalGeneration", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileRecord_WhenTypedResultCompleted_RequiresOwnedVerdict ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new CompileLifecycleExecutionTerminalRecord(
                LifecycleExecutionContractTestFactory.ExecutionId,
                DefinitionDigest(LifecycleExecutionKind.Compile),
                LifecycleExecutionContractTestFactory.Project,
                LifecycleExecutionContractTestFactory.Host,
                LifecycleExecutionContractTestFactory.StartedGeneration,
                LifecycleExecutionContractTestFactory.TerminalGeneration,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                LifecycleExecutionTerminalReason.Completed,
                ExecutionApplicationState.Applied,
                LifecycleExecutionContractTestFactory.CreateCompileResult(),
                verdict: null,
                artifactRefs: Array.Empty<ArtifactRef>()));

        Assert.Equal("Verdict", exception.ParamName);
    }

    [Theory]
    [InlineData("result")]
    [InlineData("terminalGeneration")]
    [InlineData("verdict")]
    [Trait("Size", "Small")]
    public void Deserialize_WhenRequiredNullablePropertyIsMissing_RejectsJson (
        string propertyName)
    {
        var record = new RefreshLifecycleExecutionTerminalRecord(
            LifecycleExecutionContractTestFactory.ExecutionId,
            DefinitionDigest(LifecycleExecutionKind.Refresh),
            LifecycleExecutionContractTestFactory.Project,
            LifecycleExecutionContractTestFactory.Host,
            LifecycleExecutionContractTestFactory.StartedGeneration,
            terminalGeneration: null,
            LifecycleExecutionContractTestFactory.DeadlineUtc,
            LifecycleExecutionContractTestFactory.StartedAtUtc,
            LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
            LifecycleExecutionTerminalReason.ActionFailed,
            ExecutionApplicationState.Unknown,
            result: null,
            verdict: null,
            artifactRefs: Array.Empty<ArtifactRef>());
        var json = JsonSerializer.Serialize(
            record,
            typeof(LifecycleExecutionTerminalRecord),
            IpcJsonSerializerOptions.StrictPropertyNames);
        var objectNode = JsonNode.Parse(json)!.AsObject();
        Assert.True(objectNode.Remove(propertyName));

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<LifecycleExecutionTerminalRecord>(
                objectNode.ToJsonString(),
                IpcJsonSerializerOptions.StrictPropertyNames));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WhenExecutionKindIsUnknown_RejectsJson ()
    {
        var json = JsonSerializer.Serialize(
            CreateCompletedRecord(LifecycleExecutionKind.Refresh),
            typeof(LifecycleExecutionTerminalRecord),
            IpcJsonSerializerOptions.StrictPropertyNames);
        var objectNode = JsonNode.Parse(json)!.AsObject();
        objectNode["executionKind"] = "build";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<LifecycleExecutionTerminalRecord>(
                objectNode.ToJsonString(),
                IpcJsonSerializerOptions.StrictPropertyNames));
    }

    private static LifecycleExecutionTerminalRecord CreateCompletedRecord (
        LifecycleExecutionKind kind)
    {
        return CreateCompletedRecord(
            kind,
            LifecycleExecutionContractTestFactory.TerminalGeneration);
    }

    private static LifecycleExecutionTerminalRecord CreateCompletedRecord (
        LifecycleExecutionKind kind,
        UnityEditorGenerationSnapshot terminalGeneration)
    {
        var common = new
        {
            ExecutionId = LifecycleExecutionContractTestFactory.ExecutionId,
            DefinitionDigest = DefinitionDigest(kind),
            Project = LifecycleExecutionContractTestFactory.Project,
            Host = LifecycleExecutionContractTestFactory.Host,
            StartedGeneration = LifecycleExecutionContractTestFactory.StartedGeneration,
            TerminalGeneration = terminalGeneration,
            DeadlineUtc = LifecycleExecutionContractTestFactory.DeadlineUtc,
            StartedAtUtc = LifecycleExecutionContractTestFactory.StartedAtUtc,
            CompletedAtUtc = LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
        };
        return kind switch
        {
            LifecycleExecutionKind.Refresh =>
                new RefreshLifecycleExecutionTerminalRecord(
                    common.ExecutionId,
                    common.DefinitionDigest,
                    common.Project,
                    common.Host,
                    common.StartedGeneration,
                    common.TerminalGeneration,
                    common.DeadlineUtc,
                    common.StartedAtUtc,
                    common.CompletedAtUtc,
                    LifecycleExecutionTerminalReason.Completed,
                    ExecutionApplicationState.Applied,
                    LifecycleExecutionContractTestFactory.CreateRefreshResult(),
                    verdict: null,
                    artifactRefs: Array.Empty<ArtifactRef>()),
            LifecycleExecutionKind.Compile =>
                new CompileLifecycleExecutionTerminalRecord(
                    common.ExecutionId,
                    common.DefinitionDigest,
                    common.Project,
                    common.Host,
                    common.StartedGeneration,
                    common.TerminalGeneration,
                    common.DeadlineUtc,
                    common.StartedAtUtc,
                    common.CompletedAtUtc,
                    LifecycleExecutionTerminalReason.Completed,
                    ExecutionApplicationState.Applied,
                    LifecycleExecutionContractTestFactory.CreateCompileResult(),
                    Verdict.Pass,
                    Array.Empty<ArtifactRef>()),
            LifecycleExecutionKind.PlayEnter =>
                new PlayEnterLifecycleExecutionTerminalRecord(
                    common.ExecutionId,
                    common.DefinitionDigest,
                    common.Project,
                    common.Host,
                    common.StartedGeneration,
                    common.TerminalGeneration,
                    common.DeadlineUtc,
                    common.StartedAtUtc,
                    common.CompletedAtUtc,
                    LifecycleExecutionTerminalReason.Completed,
                    ExecutionApplicationState.Applied,
                    CreatePlayEnterResult(),
                    verdict: null,
                    artifactRefs: Array.Empty<ArtifactRef>()),
            LifecycleExecutionKind.PlayExit =>
                new PlayExitLifecycleExecutionTerminalRecord(
                    common.ExecutionId,
                    common.DefinitionDigest,
                    common.Project,
                    common.Host,
                    common.StartedGeneration,
                    common.TerminalGeneration,
                    common.DeadlineUtc,
                    common.StartedAtUtc,
                    common.CompletedAtUtc,
                    LifecycleExecutionTerminalReason.Completed,
                    ExecutionApplicationState.Applied,
                    CreatePlayExitResult(),
                    verdict: null,
                    artifactRefs: Array.Empty<ArtifactRef>()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static LifecycleExecutionTerminalRecord CreateUnityExitedRecord (
        LifecycleExecutionKind kind,
        UnityEditorGenerationSnapshot? terminalGeneration = null,
        ExecutionApplicationState applicationState =
            ExecutionApplicationState.Indeterminate,
        bool includeResult = false,
        IReadOnlyList<ArtifactRef>? artifactRefs = null,
        Verdict? verdict = null)
    {
        var actualArtifactRefs =
            artifactRefs ?? Array.Empty<ArtifactRef>();
        return kind switch
        {
            LifecycleExecutionKind.Refresh =>
                new RefreshLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(kind),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    terminalGeneration,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc
                        .AddSeconds(4),
                    LifecycleExecutionTerminalReason.UnityExited,
                    applicationState,
                    includeResult
                        ? LifecycleExecutionContractTestFactory
                            .CreateRefreshResult()
                        : null,
                    verdict,
                    actualArtifactRefs),
            LifecycleExecutionKind.Compile =>
                new CompileLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(kind),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    terminalGeneration,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc
                        .AddSeconds(4),
                    LifecycleExecutionTerminalReason.UnityExited,
                    applicationState,
                    includeResult
                        ? LifecycleExecutionContractTestFactory
                            .CreateCompileResult()
                        : null,
                    verdict,
                    actualArtifactRefs),
            LifecycleExecutionKind.PlayEnter =>
                new PlayEnterLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(kind),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    terminalGeneration,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc
                        .AddSeconds(4),
                    LifecycleExecutionTerminalReason.UnityExited,
                    applicationState,
                    includeResult ? CreatePlayEnterResult() : null,
                    verdict,
                    actualArtifactRefs),
            LifecycleExecutionKind.PlayExit =>
                new PlayExitLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(kind),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    terminalGeneration,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc
                        .AddSeconds(4),
                    LifecycleExecutionTerminalReason.UnityExited,
                    applicationState,
                    includeResult ? CreatePlayExitResult() : null,
                    verdict,
                    actualArtifactRefs),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static Sha256Digest DefinitionDigest (
        LifecycleExecutionKind kind)
    {
        return LifecycleExecutionDefinitionDigest.Calculate(
            new LifecycleExecutionDefinition(kind));
    }

    private static PlayEnterLifecycleTransitionResult CreatePlayEnterResult (
        bool successful = true)
    {
        return PlayEnterLifecycleTransitionResult.FromProviderResult(
            LifecycleExecutionContractTestFactory.CreatePlayResult(
                PlayLifecycleTransitionCommand.Enter,
                successful));
    }

    private static PlayExitLifecycleTransitionResult CreatePlayExitResult (
        bool successful = true)
    {
        return PlayExitLifecycleTransitionResult.FromProviderResult(
            LifecycleExecutionContractTestFactory.CreatePlayResult(
                PlayLifecycleTransitionCommand.Exit,
                successful));
    }

    private static LifecycleExecutionTerminalRecord CreatePlayRecord (
        LifecycleExecutionKind kind,
        LifecycleExecutionTerminalReason terminalReason,
        ExecutionApplicationState applicationState,
        bool successfulResult)
    {
        var completedAtUtc =
            terminalReason == LifecycleExecutionTerminalReason.DeadlineExceeded
                ? LifecycleExecutionContractTestFactory.DeadlineUtc
                : LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4);
        return kind switch
        {
            LifecycleExecutionKind.PlayEnter =>
                new PlayEnterLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(kind),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    successfulResult
                        ? LifecycleExecutionContractTestFactory.TerminalGeneration
                        : LifecycleExecutionContractTestFactory.StartedGeneration,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    completedAtUtc,
                    terminalReason,
                    applicationState,
                    CreatePlayEnterResult(successfulResult),
                    verdict: null,
                    artifactRefs: Array.Empty<ArtifactRef>()),
            LifecycleExecutionKind.PlayExit =>
                new PlayExitLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(kind),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    successfulResult
                        ? LifecycleExecutionContractTestFactory.TerminalGeneration
                        : LifecycleExecutionContractTestFactory.StartedGeneration,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    completedAtUtc,
                    terminalReason,
                    applicationState,
                    CreatePlayExitResult(successfulResult),
                    verdict: null,
                    artifactRefs: Array.Empty<ArtifactRef>()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static LifecycleExecutionTerminalRecord
        CreateAlreadySatisfiedPlayRecord (
            LifecycleExecutionKind kind,
            ExecutionApplicationState applicationState)
    {
        var transition = kind == LifecycleExecutionKind.PlayEnter
            ? PlayLifecycleTransitionCommand.Enter
            : PlayLifecycleTransitionCommand.Exit;
        var ordinaryResult =
            LifecycleExecutionContractTestFactory.CreatePlayResult(transition);
        var providerResult = new PlayLifecycleTransitionResult(
            transition,
            transition == PlayLifecycleTransitionCommand.Enter
                ? PlayLifecycleTransitionOutcome.AlreadyEntered
                : PlayLifecycleTransitionOutcome.AlreadyExited,
            ordinaryResult.Before,
            After: ordinaryResult.Before,
            Observed: null,
            ApplicationState: null);
        return kind switch
        {
            LifecycleExecutionKind.PlayEnter =>
                new PlayEnterLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(kind),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                    LifecycleExecutionTerminalReason.Completed,
                    applicationState,
                    PlayEnterLifecycleTransitionResult.FromProviderResult(
                        providerResult),
                    verdict: null,
                    artifactRefs: Array.Empty<ArtifactRef>()),
            LifecycleExecutionKind.PlayExit =>
                new PlayExitLifecycleExecutionTerminalRecord(
                    LifecycleExecutionContractTestFactory.ExecutionId,
                    DefinitionDigest(kind),
                    LifecycleExecutionContractTestFactory.Project,
                    LifecycleExecutionContractTestFactory.Host,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    LifecycleExecutionContractTestFactory.StartedGeneration,
                    LifecycleExecutionContractTestFactory.DeadlineUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc,
                    LifecycleExecutionContractTestFactory.StartedAtUtc.AddSeconds(4),
                    LifecycleExecutionTerminalReason.Completed,
                    applicationState,
                    PlayExitLifecycleTransitionResult.FromProviderResult(
                        providerResult),
                    verdict: null,
                    artifactRefs: Array.Empty<ArtifactRef>()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
