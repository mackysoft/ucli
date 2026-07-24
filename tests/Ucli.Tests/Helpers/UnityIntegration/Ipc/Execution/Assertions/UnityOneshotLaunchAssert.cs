using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class UnityOneshotLaunchAssert
{
    public static IpcOneshotBootstrapEnvelope LaunchedOnce (
        RecordingUnityBatchmodeProcessLauncher launcher,
        ResolvedUnityProjectContext expectedUnityProject,
        DateTimeOffset? exitDeadlineReferenceUtc = null)
    {
        var invocation = Assert.Single(launcher.Invocations);
        Assert.Equal(
            UcliStoragePathResolver.ResolveUnityLogPath(expectedUnityProject.RepositoryRoot, expectedUnityProject.ProjectFingerprint),
            invocation.UnityLogPath);

        return HasOneshotBootstrapArguments(
            invocation,
            expectedUnityProject,
            exitDeadlineReferenceUtc ?? DateTimeOffset.UtcNow);
    }

    public static IpcOneshotBootstrapEnvelope LaunchedOnceWithDefaultOptions (
        RecordingUnityBatchmodeProcessLauncher launcher,
        ResolvedUnityProjectContext expectedUnityProject,
        DateTimeOffset? exitDeadlineReferenceUtc = null)
    {
        var bootstrapArguments = LaunchedOnce(launcher, expectedUnityProject, exitDeadlineReferenceUtc);
        var invocation = Assert.Single(launcher.Invocations);
        Assert.Same(UnityBatchmodeLaunchOptions.Default, invocation.LaunchOptions);
        return bootstrapArguments;
    }

    public static IpcOneshotBootstrapEnvelope LaunchedOnceWithActiveBuildProfile (
        RecordingUnityBatchmodeProcessLauncher launcher,
        ResolvedUnityProjectContext expectedUnityProject,
        string expectedActiveBuildProfilePath,
        DateTimeOffset? exitDeadlineReferenceUtc = null)
    {
        var bootstrapArguments = LaunchedOnce(launcher, expectedUnityProject, exitDeadlineReferenceUtc);
        var invocation = Assert.Single(launcher.Invocations);
        Assert.NotNull(invocation.LaunchOptions);
        Assert.Equal(expectedActiveBuildProfilePath, invocation.LaunchOptions!.ActiveBuildProfilePath!.Value);
        return bootstrapArguments;
    }

    private static IpcOneshotBootstrapEnvelope HasOneshotBootstrapArguments (
        RecordingUnityBatchmodeProcessLauncher.Invocation invocation,
        ResolvedUnityProjectContext expectedUnityProject,
        DateTimeOffset exitDeadlineReferenceUtc)
    {
        var bootstrapEnvelope = invocation.BootstrapEnvelope;
        Assert.NotEqual(Guid.Empty, bootstrapEnvelope.BootstrapId);
        Assert.Equal(Environment.ProcessId, bootstrapEnvelope.ParentProcess.ProcessId);
        Assert.True(ProcessLivenessProbe.IsSameProcess(bootstrapEnvelope.ParentProcess));
        Assert.Equal(expectedUnityProject.ProjectFingerprint, bootstrapEnvelope.ProjectFingerprint);
        Assert.False(string.IsNullOrWhiteSpace(bootstrapEnvelope.SessionToken.GetEncodedValue()));
        Assert.True(bootstrapEnvelope.ExitDeadlineUtc > exitDeadlineReferenceUtc);
        var expectedEndpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            expectedUnityProject.RepositoryRoot,
            expectedUnityProject.ProjectFingerprint);
        Assert.Equal(expectedEndpoint.Contract, bootstrapEnvelope.Endpoint);
        return bootstrapEnvelope;
    }
}
