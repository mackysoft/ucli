using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Assurance.Build;

public sealed class BuildRunProgressEventNamesTests
{
    private const string RunIdText = "fedcba98-7654-3210-fedc-ba9876543210";
    private static readonly Guid RunId = Guid.Parse(RunIdText);

    [Fact]
    [Trait("Size", "Small")]
    public void Constants_ExposePublicBuildRunEventNames ()
    {
        Assert.Equal("build.run.started", BuildRunProgressEventNames.Started);
        Assert.Equal("build.readiness.completed", BuildRunProgressEventNames.ReadinessCompleted);
        Assert.Equal("build.runner.resolved", BuildRunProgressEventNames.RunnerResolved);
        Assert.Equal("build.runner.started", BuildRunProgressEventNames.RunnerStarted);
        Assert.Equal("build.log.entry", BuildRunProgressEventNames.LogEntry);
        Assert.Equal("build.runner.completed", BuildRunProgressEventNames.RunnerCompleted);
        Assert.Equal("build.runnerResult.completed", BuildRunProgressEventNames.RunnerResultCompleted);
        Assert.Equal("build.artifacts.completed", BuildRunProgressEventNames.ArtifactsCompleted);
        Assert.Equal("build.run.completed", BuildRunProgressEventNames.Completed);
        Assert.Equal("build.diagnostic", BuildRunProgressEventNames.Diagnostic);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constants_ExposeBuildLogEntryLimits ()
    {
        Assert.Equal(64 * 1024, BuildLogEntryLimits.MaxMessageUtf8Bytes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildProgressEntry_SerializesFinalCamelCaseShape ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new BuildProgressEntry(
            RunId: RunId,
            ProfileDigest: Sha256Digest.Parse(new string('a', 64)),
            Phase: BuildRunProgressPhase.Completed,
            RunnerKind: BuildRunnerKind.BuildPipeline,
            RunnerStatus: IpcBuildReportResult.Succeeded,
            Verdict: AssuranceVerdict.Pass,
            ReportRefs:
            [
                BuildArtifactKind.Build,
                BuildArtifactKind.BuildReport,
                BuildArtifactKind.BuildOutputManifest,
                BuildArtifactKind.BuildLog,
            ],
            ErrorCode: null));

        Assert.Equal(RunIdText, json.GetProperty("runId").GetString());
        Assert.Equal(new string('a', 64), json.GetProperty("profileDigest").GetString());
        Assert.Equal("completed", json.GetProperty("phase").GetString());
        Assert.Equal("buildPipeline", json.GetProperty("runnerKind").GetString());
        Assert.Equal("succeeded", json.GetProperty("runnerStatus").GetString());
        Assert.Equal("pass", json.GetProperty("verdict").GetString());
        Assert.Equal(4, json.GetProperty("reportRefs").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("errorCode").ValueKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildProgressEntry_WithNullProfileDigest_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new BuildProgressEntry(
            RunId: RunId,
            ProfileDigest: null!,
            Phase: BuildRunProgressPhase.Started,
            RunnerKind: null,
            RunnerStatus: null,
            Verdict: null,
            ReportRefs: [],
            ErrorCode: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildLogEntry_SerializesFinalCamelCaseShape ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new BuildLogEntry(
            RunId: RunId,
            TimestampUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
            Level: BuildLogEntryLevel.Warning,
            Message: "sample warning",
            Cursor: "stream-1:42",
            Source: BuildLogEntrySource.UnityLog));

        Assert.Equal(RunIdText, json.GetProperty("runId").GetString());
        Assert.Equal("warning", json.GetProperty("level").GetString());
        Assert.Equal("sample warning", json.GetProperty("message").GetString());
        Assert.Equal("stream-1:42", json.GetProperty("cursor").GetString());
        Assert.Equal("unityLog", json.GetProperty("source").GetString());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("defaultTimestamp")]
    [InlineData("nonUtcTimestamp")]
    [InlineData("emptyMessage")]
    [InlineData("emptyCursor")]
    [InlineData("oversizedMessage")]
    public void BuildLogEntry_WithInvalidValue_ThrowsArgumentException (string invalidValue)
    {
        var timestampUtc = DateTimeOffset.Parse("2026-06-12T00:00:00+00:00");
        var message = "sample warning";
        string? cursor = "stream-1:42";

        switch (invalidValue)
        {
            case "defaultTimestamp":
                timestampUtc = default;
                break;
            case "nonUtcTimestamp":
                timestampUtc = new DateTimeOffset(2026, 6, 12, 9, 0, 0, TimeSpan.FromHours(9));
                break;
            case "emptyMessage":
                message = " ";
                break;
            case "emptyCursor":
                cursor = string.Empty;
                break;
            case "oversizedMessage":
                message = new string('x', BuildLogEntryLimits.MaxMessageUtf8Bytes + 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidValue), invalidValue, null);
        }

        Assert.Throws<ArgumentException>(() => new BuildLogEntry(
            RunId: RunId,
            TimestampUtc: timestampUtc,
            Level: BuildLogEntryLevel.Warning,
            Message: message,
            Cursor: cursor,
            Source: BuildLogEntrySource.UnityLog));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildDiagnosticEntry_SerializesFinalCamelCaseShape ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new BuildDiagnosticEntry(
            RunId: RunId,
            Code: new UcliCode("BUILD_PROGRESS_DROPPED"),
            Severity: UcliDiagnosticSeverity.Warning,
            Message: "progress dropped",
            Phase: BuildRunProgressPhase.RunnerInvocation));

        Assert.Equal(RunIdText, json.GetProperty("runId").GetString());
        Assert.Equal("BUILD_PROGRESS_DROPPED", json.GetProperty("code").GetString());
        Assert.Equal("warning", json.GetProperty("severity").GetString());
        Assert.Equal("progress dropped", json.GetProperty("message").GetString());
        Assert.Equal("runnerInvocation", json.GetProperty("phase").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildDiagnosticEntry_WithNullCode_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new BuildDiagnosticEntry(
            RunId: RunId,
            Code: null!,
            Severity: UcliDiagnosticSeverity.Warning,
            Message: "progress dropped",
            Phase: BuildRunProgressPhase.RunnerInvocation));
    }

}
