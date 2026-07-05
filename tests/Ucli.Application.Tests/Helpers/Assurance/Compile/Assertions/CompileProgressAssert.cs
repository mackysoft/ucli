using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance;

namespace MackySoft.Ucli.Application.Tests;

internal static class CompileProgressAssert
{
    public static void SuccessfulCompileProgressPayloads (CollectingCommandProgressSink progressSink)
    {
        var startedEntry = Assert.IsType<CompileStartedEntry>(progressSink.Entries[0].Payload);
        Assert.Equal("run-1", startedEntry.RunId);
        Assert.Equal("project-fingerprint", startedEntry.ProjectFingerprint);
        Assert.Equal("auto", startedEntry.RequestedMode);
        Assert.Equal("oneshot", startedEntry.ResolvedMode);
        Assert.Equal("transientProbe", startedEntry.SessionKind);
        Assert.Equal(10000, startedEntry.TimeoutMilliseconds);
        var refreshEntry = Assert.IsType<CompileRefreshStartedEntry>(progressSink.Entries[1].Payload);
        Assert.Equal("assetDatabaseRefresh", refreshEntry.RefreshOrigin);
        Assert.Equal("hostDispatch", refreshEntry.ObservationSource);
        var completedEntry = Assert.IsType<CompileCompletedEntry>(progressSink.Entries[2].Payload);
        Assert.Equal(CompileVerdictValues.Pass, completedEntry.Verdict);
        Assert.Equal(0, completedEntry.ErrorCount);
    }

    public static void TimeoutRecoveredArtifactProgressPayload (
        CollectingCommandProgressSink progressSink,
        string expectedSummaryJsonPath)
    {
        var recoveredEntry = Assert.IsType<CompileRecoveredEntry>(progressSink.Entries[2].Payload);
        Assert.Equal("run-1", recoveredEntry.RunId);
        Assert.Equal(expectedSummaryJsonPath, recoveredEntry.SummaryJsonPath);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout.Value, recoveredEntry.DispatchFailureCode);
        Assert.Equal(1, recoveredEntry.PollAttempts);
    }

    public static void StartupCompilerDiagnosticProgressPayload (CollectingCommandProgressSink progressSink)
    {
        var diagnosticEntry = Assert.IsType<CompileDiagnosticEntry>(progressSink.Entries[2].Payload);
        Assert.Equal("run-1", diagnosticEntry.RunId);
        Assert.Equal("diagnosticsRead", diagnosticEntry.RefreshOrigin);
        Assert.Equal("CS0246", diagnosticEntry.PrimaryDiagnostic!.Code);
    }
}
