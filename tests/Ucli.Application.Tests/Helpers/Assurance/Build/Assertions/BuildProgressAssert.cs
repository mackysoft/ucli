using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class BuildProgressAssert
{
    public static void BuildPipelineSuccessProgressPayloads (
        CollectingCommandProgressSink progressSink,
        Guid expectedRunId,
        string expectedProfileDigest)
    {
        var startedEntry = Assert.IsType<BuildProgressEntry>(progressSink.Entries[0].Payload);
        Assert.Equal(expectedRunId, startedEntry.RunId);
        Assert.Equal(expectedProfileDigest, startedEntry.ProfileDigest);
        Assert.Equal("started", startedEntry.Phase);
        Assert.Null(startedEntry.RunnerKind);
        Assert.Empty(startedEntry.ReportRefs);

        var runnerCompletedEntry = Assert.IsType<BuildProgressEntry>(progressSink.Entries[4].Payload);
        Assert.Equal("runnerResult", runnerCompletedEntry.Phase);
        Assert.Equal("buildPipeline", runnerCompletedEntry.RunnerKind);
        Assert.Equal("succeeded", runnerCompletedEntry.RunnerStatus);

        var completedEntry = Assert.IsType<BuildProgressEntry>(progressSink.Entries[7].Payload);
        Assert.Equal(expectedRunId, completedEntry.RunId);
        Assert.Equal("completed", completedEntry.Phase);
        Assert.Equal("pass", completedEntry.Verdict);
        Assert.Equal(
            [
                BuildReportRefs.Build,
                BuildReportRefs.BuildReport,
                BuildReportRefs.BuildOutputManifest,
                BuildReportRefs.BuildLog,
            ],
            completedEntry.ReportRefs);
    }

    public static void ExecuteMethodRunnerKindPreserved (CollectingCommandProgressSink progressSink)
    {
        var executeMethodRunnerResolved = Assert.IsType<BuildProgressEntry>(progressSink.Entries[2].Payload);
        Assert.Equal("executeMethod", executeMethodRunnerResolved.RunnerKind);
        var executeMethodRunnerCompleted = Assert.IsType<BuildProgressEntry>(progressSink.Entries[4].Payload);
        Assert.Equal("executeMethod", executeMethodRunnerCompleted.RunnerKind);
        Assert.Equal("succeeded", executeMethodRunnerCompleted.RunnerStatus);
    }

    public static void RunnerInvocationFailureDiagnosticEmitted (
        CollectingCommandProgressSink progressSink,
        Guid expectedRunId)
    {
        var diagnostic = Assert.IsType<BuildDiagnosticEntry>(progressSink.Entries[1].Payload);
        Assert.Equal(expectedRunId, diagnostic.RunId);
        Assert.Equal(BuildErrorCodes.BuildRunnerInvocationFailed.Value, diagnostic.Code);
        Assert.Equal(IpcExecuteDiagnosticSeverityNames.Error, diagnostic.Severity);
        Assert.Equal("runnerInvocation", diagnostic.Phase);
    }
}
