using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests;

internal static class CompileProgressAssert
{
    public static void SuccessfulCompileProgressPayloads (CollectingCommandProgressSink progressSink)
    {
        var startedEntry = Assert.IsType<CompileStartedEntry>(progressSink.Entries[0].Payload);
        Assert.Equal(ExecutionId, startedEntry.ExecutionId);
        Assert.Equal(ProjectFingerprintTestFactory.Create("project-fingerprint"), startedEntry.ProjectFingerprint);
        Assert.Equal(AssuranceRequestedExecutionMode.Auto, startedEntry.RequestedMode);
        Assert.Equal(AssuranceResolvedExecutionMode.Oneshot, startedEntry.ResolvedMode);
        Assert.Equal(AssuranceSessionKind.TransientProbe, startedEntry.SessionKind);
        Assert.Equal(10000, startedEntry.TimeoutMilliseconds);
        var completedEntry = Assert.IsType<CompileCompletedEntry>(progressSink.Entries[1].Payload);
        Assert.Equal(ExecutionId, completedEntry.ExecutionId);
        Assert.Equal(Verdict.Pass, completedEntry.Verdict);
        Assert.Equal(0, completedEntry.ErrorCount);
    }
}
