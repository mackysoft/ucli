using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests;

internal static class CompileProgressAssert
{
    public static void SuccessfulCompileProgressPayloads (CollectingCommandProgressSink progressSink)
    {
        var startedEntry = Assert.IsType<CompileStartedEntry>(progressSink.Entries[0].Payload);
        Assert.Equal(RunId, startedEntry.RunId);
        Assert.Equal(ProjectFingerprintTestFactory.Create("project-fingerprint"), startedEntry.ProjectFingerprint);
        Assert.Equal(AssuranceRequestedExecutionMode.Auto, startedEntry.RequestedMode);
        Assert.Equal(AssuranceResolvedExecutionMode.Oneshot, startedEntry.ResolvedMode);
        Assert.Equal(AssuranceSessionKind.TransientProbe, startedEntry.SessionKind);
        Assert.Equal(10000, startedEntry.TimeoutMilliseconds);
        var refreshEntry = Assert.IsType<CompileRefreshStartedEntry>(progressSink.Entries[1].Payload);
        Assert.Equal(CompileRefreshOrigin.AssetDatabaseRefresh, refreshEntry.RefreshOrigin);
        Assert.Equal("hostDispatch", refreshEntry.ObservationSource);
        var completedEntry = Assert.IsType<CompileCompletedEntry>(progressSink.Entries[2].Payload);
        Assert.Equal(AssuranceVerdict.Pass, completedEntry.Verdict);
        Assert.Equal(0, completedEntry.ErrorCount);
    }

    public static void TimeoutRecoveredArtifactProgressPayload (
        CollectingCommandProgressSink progressSink,
        string expectedSummaryJsonPath)
    {
        var recoveredEntry = Assert.IsType<CompileRecoveredEntry>(progressSink.Entries[2].Payload);
        Assert.Equal(RunId, recoveredEntry.RunId);
        Assert.Equal(expectedSummaryJsonPath, recoveredEntry.SummaryJsonPath);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout.Value, recoveredEntry.DispatchFailureCode);
        Assert.Equal(1, recoveredEntry.PollAttempts);
    }

    public static void StartupCompilerDiagnosticProgressPayload (CollectingCommandProgressSink progressSink)
    {
        var diagnosticEntry = Assert.IsType<CompileDiagnosticEntry>(progressSink.Entries[2].Payload);
        Assert.Equal(RunId, diagnosticEntry.RunId);
        Assert.Equal(CompileRefreshOrigin.DiagnosticsRead, diagnosticEntry.RefreshOrigin);
        Assert.Equal("CS0246", diagnosticEntry.PrimaryDiagnostic!.Code);
    }
}
