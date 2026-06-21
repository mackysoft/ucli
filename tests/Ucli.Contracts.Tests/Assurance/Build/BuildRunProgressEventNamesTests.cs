using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Assurance.Build;

public sealed class BuildRunProgressEventNamesTests
{
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
    public void Constants_ExposePublicBuildRunPhaseNames ()
    {
        Assert.Equal("started", BuildRunProgressPhaseNames.Started);
        Assert.Equal("readiness", BuildRunProgressPhaseNames.Readiness);
        Assert.Equal("runnerResolution", BuildRunProgressPhaseNames.RunnerResolution);
        Assert.Equal("runnerInvocation", BuildRunProgressPhaseNames.RunnerInvocation);
        Assert.Equal("runnerResult", BuildRunProgressPhaseNames.RunnerResult);
        Assert.Equal("artifactAccounting", BuildRunProgressPhaseNames.ArtifactAccounting);
        Assert.Equal("completed", BuildRunProgressPhaseNames.Completed);
        Assert.Equal(
            new[]
            {
                BuildRunProgressPhaseNames.Started,
                BuildRunProgressPhaseNames.Readiness,
                BuildRunProgressPhaseNames.RunnerResolution,
                BuildRunProgressPhaseNames.RunnerInvocation,
                BuildRunProgressPhaseNames.RunnerResult,
                BuildRunProgressPhaseNames.ArtifactAccounting,
                BuildRunProgressPhaseNames.Completed,
            },
            BuildRunProgressPhaseNames.All);
        AssertClosedSetIsReadOnly(BuildRunProgressPhaseNames.All);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constants_ExposePublicBuildLogEntryLevelNames ()
    {
        Assert.Equal("trace", BuildLogEntryLevelNames.Trace);
        Assert.Equal("debug", BuildLogEntryLevelNames.Debug);
        Assert.Equal("info", BuildLogEntryLevelNames.Info);
        Assert.Equal("warning", BuildLogEntryLevelNames.Warning);
        Assert.Equal("error", BuildLogEntryLevelNames.Error);
        Assert.Equal(
            new[]
            {
                BuildLogEntryLevelNames.Trace,
                BuildLogEntryLevelNames.Debug,
                BuildLogEntryLevelNames.Info,
                BuildLogEntryLevelNames.Warning,
                BuildLogEntryLevelNames.Error,
            },
            BuildLogEntryLevelNames.All);
        AssertClosedSetIsReadOnly(BuildLogEntryLevelNames.All);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constants_ExposePublicBuildLogEntrySourceNames ()
    {
        Assert.Equal("unityLog", BuildLogEntrySourceNames.UnityLog);
        Assert.Equal("ucli", BuildLogEntrySourceNames.Ucli);
        Assert.Equal(
            new[]
            {
                BuildLogEntrySourceNames.UnityLog,
                BuildLogEntrySourceNames.Ucli,
            },
            BuildLogEntrySourceNames.All);
        AssertClosedSetIsReadOnly(BuildLogEntrySourceNames.All);
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
            RunId: "build-run-1",
            ProfileDigest: new string('a', 64),
            Phase: BuildRunProgressPhaseNames.Completed,
            RunnerKind: "buildPipeline",
            RunnerStatus: "succeeded",
            Verdict: "pass",
            ReportRefs: ["build", "buildReport", "buildOutputManifest", "buildLog"],
            ErrorCode: null));

        Assert.Equal("build-run-1", json.GetProperty("runId").GetString());
        Assert.Equal(new string('a', 64), json.GetProperty("profileDigest").GetString());
        Assert.Equal(BuildRunProgressPhaseNames.Completed, json.GetProperty("phase").GetString());
        Assert.Equal("buildPipeline", json.GetProperty("runnerKind").GetString());
        Assert.Equal("succeeded", json.GetProperty("runnerStatus").GetString());
        Assert.Equal("pass", json.GetProperty("verdict").GetString());
        Assert.Equal(4, json.GetProperty("reportRefs").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("errorCode").ValueKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildLogEntry_SerializesFinalCamelCaseShape ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new BuildLogEntry(
            RunId: "build-run-1",
            TimestampUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
            Level: BuildLogEntryLevelNames.Warning,
            Message: "sample warning",
            Cursor: "stream-1:42",
            Source: BuildLogEntrySourceNames.UnityLog));

        Assert.Equal("build-run-1", json.GetProperty("runId").GetString());
        Assert.Equal(BuildLogEntryLevelNames.Warning, json.GetProperty("level").GetString());
        Assert.Equal("sample warning", json.GetProperty("message").GetString());
        Assert.Equal("stream-1:42", json.GetProperty("cursor").GetString());
        Assert.Equal(BuildLogEntrySourceNames.UnityLog, json.GetProperty("source").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildDiagnosticEntry_SerializesFinalCamelCaseShape ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new BuildDiagnosticEntry(
            RunId: "build-run-1",
            Code: "BUILD_PROGRESS_DROPPED",
            Severity: "warning",
            Message: "progress dropped",
            Phase: BuildRunProgressPhaseNames.RunnerInvocation));

        Assert.Equal("build-run-1", json.GetProperty("runId").GetString());
        Assert.Equal("BUILD_PROGRESS_DROPPED", json.GetProperty("code").GetString());
        Assert.Equal("warning", json.GetProperty("severity").GetString());
        Assert.Equal("progress dropped", json.GetProperty("message").GetString());
        Assert.Equal(BuildRunProgressPhaseNames.RunnerInvocation, json.GetProperty("phase").GetString());
    }

    private static void AssertClosedSetIsReadOnly (IReadOnlyList<string> values)
    {
        var mutableView = Assert.IsAssignableFrom<IList<string>>(values);
        var original = values[0];

        Assert.Throws<NotSupportedException>(() => mutableView[0] = "changed");
        Assert.Equal(original, values[0]);
    }
}
