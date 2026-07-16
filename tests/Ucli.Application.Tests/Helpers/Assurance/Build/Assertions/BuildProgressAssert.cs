using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class BuildProgressAssert
{
    public static void BuildPipelineSuccessProgressPayloads (
        CollectingCommandProgressSink progressSink,
        Guid expectedRunId,
        Sha256Digest expectedProfileDigest)
    {
        var startedEntry = Assert.IsType<BuildProgressEntry>(progressSink.Entries[0].Payload);
        Assert.Equal(expectedRunId, startedEntry.RunId);
        Assert.Equal(expectedProfileDigest, startedEntry.ProfileDigest);
        Assert.Equal(BuildRunProgressPhase.Started, startedEntry.Phase);
        Assert.Null(startedEntry.RunnerKind);
        Assert.Empty(startedEntry.ReportRefs);

        var runnerCompletedEntry = Assert.IsType<BuildProgressEntry>(progressSink.Entries[4].Payload);
        Assert.Equal(BuildRunProgressPhase.RunnerResult, runnerCompletedEntry.Phase);
        Assert.Equal(BuildRunnerKind.BuildPipeline, runnerCompletedEntry.RunnerKind);
        Assert.Equal(IpcBuildReportResult.Succeeded, runnerCompletedEntry.RunnerStatus);

        var completedEntry = Assert.IsType<BuildProgressEntry>(progressSink.Entries[7].Payload);
        Assert.Equal(expectedRunId, completedEntry.RunId);
        Assert.Equal(BuildRunProgressPhase.Completed, completedEntry.Phase);
        Assert.Equal(AssuranceVerdict.Pass, completedEntry.Verdict);
        Assert.Equal(
            [
                BuildArtifactKind.Build,
                BuildArtifactKind.BuildReport,
                BuildArtifactKind.BuildOutputManifest,
                BuildArtifactKind.BuildLog,
            ],
            completedEntry.ReportRefs);
    }

    public static void ExecuteMethodRunnerKindPreserved (CollectingCommandProgressSink progressSink)
    {
        var executeMethodRunnerResolved = Assert.IsType<BuildProgressEntry>(progressSink.Entries[2].Payload);
        Assert.Equal(BuildRunnerKind.ExecuteMethod, executeMethodRunnerResolved.RunnerKind);
        var executeMethodRunnerCompleted = Assert.IsType<BuildProgressEntry>(progressSink.Entries[4].Payload);
        Assert.Equal(BuildRunnerKind.ExecuteMethod, executeMethodRunnerCompleted.RunnerKind);
        Assert.Equal(IpcBuildReportResult.Succeeded, executeMethodRunnerCompleted.RunnerStatus);
    }

    public static void RunnerInvocationFailureDiagnosticEmitted (
        CollectingCommandProgressSink progressSink,
        Guid expectedRunId)
    {
        var diagnostic = Assert.IsType<BuildDiagnosticEntry>(progressSink.Entries[1].Payload);
        Assert.Equal(expectedRunId, diagnostic.RunId);
        Assert.Equal(BuildErrorCodes.BuildRunnerInvocationFailed, diagnostic.Code);
        Assert.Equal(UcliDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(BuildRunProgressPhase.RunnerInvocation, diagnostic.Phase);
    }
}
