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
    public void BuildProgressEntry_SerializesFinalCamelCaseShape ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new BuildProgressEntry(
            RunId: "build-run-1",
            ProfileDigest: new string('a', 64),
            Phase: "completed",
            RunnerKind: "buildPipeline",
            RunnerStatus: "succeeded",
            Verdict: "pass",
            ReportRefs: ["build", "buildReport", "buildOutputManifest", "buildLog"],
            ErrorCode: null));

        Assert.Equal("build-run-1", json.GetProperty("runId").GetString());
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
    public void BuildLogEntry_SerializesFinalCamelCaseShape ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new BuildLogEntry(
            RunId: "build-run-1",
            TimestampUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
            Level: "warning",
            Message: "sample warning",
            Cursor: "stream-1:42",
            Source: "unityLog"));

        Assert.Equal("build-run-1", json.GetProperty("runId").GetString());
        Assert.Equal("warning", json.GetProperty("level").GetString());
        Assert.Equal("sample warning", json.GetProperty("message").GetString());
        Assert.Equal("stream-1:42", json.GetProperty("cursor").GetString());
        Assert.Equal("unityLog", json.GetProperty("source").GetString());
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
            Phase: "runnerInvocation"));

        Assert.Equal("build-run-1", json.GetProperty("runId").GetString());
        Assert.Equal("BUILD_PROGRESS_DROPPED", json.GetProperty("code").GetString());
        Assert.Equal("warning", json.GetProperty("severity").GetString());
        Assert.Equal("progress dropped", json.GetProperty("message").GetString());
        Assert.Equal("runnerInvocation", json.GetProperty("phase").GetString());
    }
}
