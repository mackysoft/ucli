using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Common.IpcBuildContractSerializationTestSupport;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Build;

public sealed class IpcBuildNormalizedContractTests
{
    [Theory]
    [InlineData("Outputs")]
    [InlineData("BuildReport")]
    [Trait("Size", "Small")]
    public void IpcBuildRunnerResultArtifact_WhenBuildPipelineDeclaresRunnerOutputEvidence_Throws (string parameterName)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcBuildRunnerResultArtifact(
            Source: IpcBuildRunnerResultSource.BuildPipelineBuildReport,
            Status: IpcBuildReportResult.Succeeded,
            DurationMilliseconds: 0,
            ErrorCount: 0,
            WarningCount: 0,
            Diagnostics: [],
            Outputs: parameterName == "Outputs" ? [new BuildRunnerOutputPath("player.txt")] : [],
            BuildReport: parameterName == "BuildReport"
                ? new IpcBuildRunnerResultBuildReport(new BuildRunnerOutputPath("reports/build-report.json"))
                : null));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunnerResultArtifact_WhenSuccessfulUcliRunnerHasNoOutputs_Throws ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcBuildRunnerResultArtifact(
            Source: IpcBuildRunnerResultSource.UcliBuildRunnerResult,
            Status: IpcBuildReportResult.Succeeded,
            DurationMilliseconds: 0,
            ErrorCount: 0,
            WarningCount: 0,
            Diagnostics: [],
            Outputs: [],
            BuildReport: null));

        Assert.Equal("Outputs", exception.ParamName);
    }

    [Theory]
    [InlineData("DurationMilliseconds")]
    [InlineData("ErrorCount")]
    [InlineData("WarningCount")]
    [Trait("Size", "Small")]
    public void IpcBuildRunnerResultArtifact_WhenSummaryValueIsNegative_Throws (string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => parameterName switch
        {
            "DurationMilliseconds" => CreateRunnerResult(durationMilliseconds: -1, errorCount: 0, warningCount: 0),
            "ErrorCount" => CreateRunnerResult(durationMilliseconds: 0, errorCount: -1, warningCount: 0),
            "WarningCount" => CreateRunnerResult(durationMilliseconds: 0, errorCount: 0, warningCount: -1),
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, null),
        });

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData("DurationMilliseconds")]
    [InlineData("TotalSizeBytes")]
    [InlineData("ErrorCount")]
    [InlineData("WarningCount")]
    [Trait("Size", "Small")]
    public void IpcBuildReportArtifact_WhenSummaryValueIsNegative_Throws (string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => parameterName switch
        {
            "DurationMilliseconds" => CreateBuildReportWithSummary(durationMilliseconds: -1, totalSizeBytes: 0, errorCount: 0, warningCount: 0),
            "TotalSizeBytes" => CreateBuildReportWithSummary(durationMilliseconds: 0, totalSizeBytes: -1, errorCount: 0, warningCount: 0),
            "ErrorCount" => CreateBuildReportWithSummary(durationMilliseconds: 0, totalSizeBytes: 0, errorCount: -1, warningCount: 0),
            "WarningCount" => CreateBuildReportWithSummary(durationMilliseconds: 0, totalSizeBytes: 0, errorCount: 0, warningCount: -1),
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, null),
        });

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildReportArtifact_WhenSchemaVersionIsUnsupported_Throws ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new IpcBuildReportArtifact(
            SchemaVersion: 2,
            Result: IpcBuildReportResult.Succeeded,
            UnityBuildTarget: "StandaloneLinux64",
            OutputPath: string.Empty,
            DurationMilliseconds: 0,
            TotalSizeBytes: 0,
            ErrorCount: 0,
            WarningCount: 0,
            Steps: [],
            Messages: []));

        Assert.Equal("SchemaVersion", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [Trait("Size", "Small")]
    public void IpcBuildReportArtifact_WhenUnityBuildTargetIsMissing_Throws (string? unityBuildTarget)
    {
        Assert.ThrowsAny<ArgumentException>(() => new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: IpcBuildReportResult.Succeeded,
            UnityBuildTarget: unityBuildTarget!,
            OutputPath: string.Empty,
            DurationMilliseconds: 0,
            TotalSizeBytes: 0,
            ErrorCount: 0,
            WarningCount: 0,
            Steps: [],
            Messages: []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildReportArtifact_WhenOutputPathIsNull_Throws ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: IpcBuildReportResult.Succeeded,
            UnityBuildTarget: "StandaloneLinux64",
            OutputPath: null!,
            DurationMilliseconds: 0,
            TotalSizeBytes: 0,
            ErrorCount: 0,
            WarningCount: 0,
            Steps: [],
            Messages: []));

        Assert.Equal("OutputPath", exception.ParamName);
    }

    [Theory]
    [InlineData("Steps")]
    [InlineData("Messages")]
    [Trait("Size", "Small")]
    public void IpcBuildReportArtifact_WhenCollectionIsNull_Throws (string parameterName)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => parameterName switch
        {
            "Steps" => CreateBuildReportWithItems(steps: null!, messages: []),
            "Messages" => CreateBuildReportWithItems(steps: [], messages: null!),
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, null),
        });

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData("Steps")]
    [InlineData("Messages")]
    [Trait("Size", "Small")]
    public void IpcBuildReportArtifact_WhenCollectionContainsNull_Throws (string parameterName)
    {
        var exception = Assert.Throws<ArgumentException>(() => parameterName switch
        {
            "Steps" => CreateBuildReportWithItems(steps: [null!], messages: []),
            "Messages" => CreateBuildReportWithItems(steps: [], messages: [null!]),
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, null),
        });

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildReportArtifact_WhenResultIsUnknownAndOutputPathIsEmpty_PreservesValues ()
    {
        var report = new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: IpcBuildReportResult.Unknown,
            UnityBuildTarget: "StandaloneLinux64",
            OutputPath: string.Empty,
            DurationMilliseconds: 0,
            TotalSizeBytes: 0,
            ErrorCount: 0,
            WarningCount: 0,
            Steps: [],
            Messages: []);

        Assert.Equal(IpcBuildReportResult.Unknown, report.Result);
        Assert.Equal(string.Empty, report.OutputPath);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("DurationMilliseconds")]
    [InlineData("Depth")]
    [InlineData("MessageCount")]
    [Trait("Size", "Small")]
    public void IpcBuildReportStep_WhenInputIsInvalid_Throws (string parameterName)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => parameterName switch
        {
            "Name" => new IpcBuildReportStep(null!, 0, 0, 0),
            "DurationMilliseconds" => new IpcBuildReportStep("Build player", -1, 0, 0),
            "Depth" => new IpcBuildReportStep("Build player", 0, -1, 0),
            "MessageCount" => new IpcBuildReportStep("Build player", 0, 0, -1),
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, null),
        });

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildReportStep_WhenNameIsEmpty_PreservesName ()
    {
        var step = new IpcBuildReportStep(string.Empty, 0, 0, 0);

        Assert.Equal(string.Empty, step.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [Trait("Size", "Small")]
    public void IpcBuildReportMessage_WhenTypeIsMissing_Throws (string? type)
    {
        Assert.ThrowsAny<ArgumentException>(() => new IpcBuildReportMessage(type!, string.Empty));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildReportMessage_WhenContentIsNull_Throws ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcBuildReportMessage("warning", null!));

        Assert.Equal("Content", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildReportMessage_WhenContentIsEmpty_PreservesContent ()
    {
        var message = new IpcBuildReportMessage("warning", string.Empty);

        Assert.Equal(string.Empty, message.Content);
    }

    [Theory]
    [InlineData("EntryCount")]
    [InlineData("ErrorCount")]
    [InlineData("WarningCount")]
    [Trait("Size", "Small")]
    public void IpcBuildLogSummary_WhenCountIsNegative_Throws (string parameterName)
    {
        var window = CreateLogWindow();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => parameterName switch
        {
            "EntryCount" => new IpcBuildLogSummary(-1, 0, 0, IpcBuildLogCompletionReason.Completed, window),
            "ErrorCount" => new IpcBuildLogSummary(0, -1, 0, IpcBuildLogCompletionReason.Completed, window),
            "WarningCount" => new IpcBuildLogSummary(0, 0, -1, IpcBuildLogCompletionReason.Completed, window),
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, null),
        });

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData("StartedAtUtc")]
    [InlineData("CompletedAtUtc")]
    [InlineData("StartedAtUtcOffset")]
    [InlineData("CompletedAtUtcOffset")]
    [InlineData("CompletedBeforeStarted")]
    [Trait("Size", "Small")]
    public void IpcBuildLogWindow_WhenTimestampIsInvalid_Throws (string invalidInput)
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch;
        var completedAtUtc = startedAtUtc.AddSeconds(1);
        var nonUtc = new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.FromHours(1));
        var exception = Assert.ThrowsAny<ArgumentException>(() => invalidInput switch
        {
            "StartedAtUtc" => new IpcBuildLogWindow(default, completedAtUtc, null, null),
            "CompletedAtUtc" => new IpcBuildLogWindow(startedAtUtc, default, null, null),
            "StartedAtUtcOffset" => new IpcBuildLogWindow(nonUtc, completedAtUtc, null, null),
            "CompletedAtUtcOffset" => new IpcBuildLogWindow(startedAtUtc, nonUtc, null, null),
            "CompletedBeforeStarted" => new IpcBuildLogWindow(completedAtUtc, startedAtUtc, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidInput), invalidInput, null),
        });

        Assert.Equal(
            invalidInput.StartsWith("StartedAtUtc", StringComparison.Ordinal) ? "StartedAtUtc" : "CompletedAtUtc",
            exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildLogWindow_CursorsAreExplicitConstructorState ()
    {
        var parameters = Assert.Single(typeof(IpcBuildLogWindow).GetConstructors()).GetParameters();

        Assert.False(Assert.Single(parameters, static parameter => parameter.Name == "CursorStart").HasDefaultValue);
        Assert.False(Assert.Single(parameters, static parameter => parameter.Name == "CursorEnd").HasDefaultValue);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildLogWindow_WhenCursorsBelongToDifferentStreams_Throws ()
    {
        var startedAtUtc = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<ArgumentException>(() => new IpcBuildLogWindow(
            startedAtUtc,
            startedAtUtc.AddSeconds(1),
            IpcLogCursor.Create(Guid.NewGuid(), 1),
            IpcLogCursor.Create(Guid.NewGuid(), 2)));

        Assert.Equal("CursorEnd", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildLogWindow_WhenEndCursorPrecedesStartCursor_Throws ()
    {
        var startedAtUtc = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
        var streamId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new IpcBuildLogWindow(
            startedAtUtc,
            startedAtUtc.AddSeconds(1),
            IpcLogCursor.Create(streamId, 2),
            IpcLogCursor.Create(streamId, 1)));

        Assert.Equal("CursorEnd", exception.ParamName);
    }

    [Theory]
    [InlineData("LifecycleBefore")]
    [InlineData("LifecycleAfter")]
    [InlineData("DirtyStateAfter")]
    [Trait("Size", "Small")]
    public void IpcUnityBuildProfileApplyAudit_WhenObservationIsNull_Throws (string parameterName)
    {
        var lifecycle = CreateBuildLifecycleSnapshot(1, canAcceptExecutionRequests: true);
        var dirtyState = new IpcBuildDirtyState(
            Dirty: false,
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items: []);
        var exception = Assert.Throws<ArgumentNullException>(() => parameterName switch
        {
            "LifecycleBefore" => new IpcUnityBuildProfileApplyAudit(true, null!, lifecycle, dirtyState),
            "LifecycleAfter" => new IpcUnityBuildProfileApplyAudit(true, lifecycle, null!, dirtyState),
            "DirtyStateAfter" => new IpcUnityBuildProfileApplyAudit(true, lifecycle, lifecycle, null!),
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, null),
        });

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunResponse_RunnerResultIsConstructorBoundGetOnlyState ()
    {
        var constructor = Assert.Single(typeof(IpcBuildRunResponse).GetConstructors());
        var parameter = Assert.Single(
            constructor.GetParameters(),
            static candidate => candidate.Name == "RunnerResult");

        Assert.False(parameter.HasDefaultValue);
        Assert.Null(typeof(IpcBuildRunResponse).GetProperty(nameof(IpcBuildRunResponse.RunnerResult))!.SetMethod);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserializeBuildReport_WhenNestedContractIsInvalid_ReturnsDeserializeFailed ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "schemaVersion": 1,
              "result": "succeeded",
              "unityBuildTarget": "StandaloneLinux64",
              "outputPath": "",
              "durationMilliseconds": 0,
              "totalSizeBytes": 0,
              "errorCount": 0,
              "warningCount": 0,
              "steps": [{ "name": "Build player", "durationMilliseconds": -1, "depth": 0, "messageCount": 0 }],
              "messages": []
            }
            """);

        var succeeded = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out IpcBuildReportArtifact _,
            out var error);

        Assert.False(succeeded);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    private static IpcBuildRunnerResultArtifact CreateRunnerResult (
        long durationMilliseconds,
        int errorCount,
        int warningCount)
    {
        return new IpcBuildRunnerResultArtifact(
            Source: IpcBuildRunnerResultSource.UcliBuildRunnerResult,
            Status: IpcBuildReportResult.Succeeded,
            DurationMilliseconds: durationMilliseconds,
            ErrorCount: errorCount,
            WarningCount: warningCount,
            Diagnostics: [],
            Outputs: [new BuildRunnerOutputPath("player.txt")],
            BuildReport: null);
    }

    private static IpcBuildReportArtifact CreateBuildReportWithSummary (
        long durationMilliseconds,
        long totalSizeBytes,
        int errorCount,
        int warningCount)
    {
        return new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: IpcBuildReportResult.Succeeded,
            UnityBuildTarget: "StandaloneLinux64",
            OutputPath: string.Empty,
            DurationMilliseconds: durationMilliseconds,
            TotalSizeBytes: totalSizeBytes,
            ErrorCount: errorCount,
            WarningCount: warningCount,
            Steps: [],
            Messages: []);
    }

    private static IpcBuildReportArtifact CreateBuildReportWithItems (
        IReadOnlyList<IpcBuildReportStep> steps,
        IReadOnlyList<IpcBuildReportMessage> messages)
    {
        return new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: IpcBuildReportResult.Succeeded,
            UnityBuildTarget: "StandaloneLinux64",
            OutputPath: string.Empty,
            DurationMilliseconds: 0,
            TotalSizeBytes: 0,
            ErrorCount: 0,
            WarningCount: 0,
            Steps: steps,
            Messages: messages);
    }

    private static IpcBuildLogWindow CreateLogWindow ()
    {
        return new IpcBuildLogWindow(
            StartedAtUtc: DateTimeOffset.UnixEpoch,
            CompletedAtUtc: DateTimeOffset.UnixEpoch,
            CursorStart: null,
            CursorEnd: null);
    }
}
