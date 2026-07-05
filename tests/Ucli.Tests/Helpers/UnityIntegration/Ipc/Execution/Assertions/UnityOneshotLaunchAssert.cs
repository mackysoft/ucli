using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class UnityOneshotLaunchAssert
{
    public static IpcOneshotBootstrapArguments LaunchedOnce (
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

    public static IpcOneshotBootstrapArguments LaunchedOnceWithDefaultOptions (
        RecordingUnityBatchmodeProcessLauncher launcher,
        ResolvedUnityProjectContext expectedUnityProject,
        DateTimeOffset? exitDeadlineReferenceUtc = null)
    {
        var bootstrapArguments = LaunchedOnce(launcher, expectedUnityProject, exitDeadlineReferenceUtc);
        var invocation = Assert.Single(launcher.Invocations);
        Assert.Same(UnityBatchmodeLaunchOptions.Default, invocation.LaunchOptions);
        return bootstrapArguments;
    }

    public static IpcOneshotBootstrapArguments LaunchedOnceWithActiveBuildProfile (
        RecordingUnityBatchmodeProcessLauncher launcher,
        ResolvedUnityProjectContext expectedUnityProject,
        string expectedActiveBuildProfilePath,
        DateTimeOffset? exitDeadlineReferenceUtc = null)
    {
        var bootstrapArguments = LaunchedOnce(launcher, expectedUnityProject, exitDeadlineReferenceUtc);
        var invocation = Assert.Single(launcher.Invocations);
        Assert.NotNull(invocation.LaunchOptions);
        Assert.Equal(expectedActiveBuildProfilePath, invocation.LaunchOptions!.ActiveBuildProfilePath);
        return bootstrapArguments;
    }

    private static IpcOneshotBootstrapArguments HasOneshotBootstrapArguments (
        RecordingUnityBatchmodeProcessLauncher.Invocation invocation,
        ResolvedUnityProjectContext expectedUnityProject,
        DateTimeOffset exitDeadlineReferenceUtc)
    {
        var bootstrapArguments = Assert.IsType<IpcOneshotBootstrapArguments>(invocation.BootstrapArguments);
        Assert.Equal(Environment.ProcessId, bootstrapArguments.ParentProcessId);
        Assert.False(string.IsNullOrWhiteSpace(bootstrapArguments.SessionToken));
        Assert.True(bootstrapArguments.ExitDeadlineUtc > exitDeadlineReferenceUtc);
        var expectedEndpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            expectedUnityProject.RepositoryRoot,
            expectedUnityProject.ProjectFingerprint);
        Assert.Equal(ContractLiteralCodec.ToValue(expectedEndpoint.TransportKind), bootstrapArguments.EndpointTransportKind);
        Assert.Equal(expectedEndpoint.Address, bootstrapArguments.EndpointAddress);
        return bootstrapArguments;
    }
}
