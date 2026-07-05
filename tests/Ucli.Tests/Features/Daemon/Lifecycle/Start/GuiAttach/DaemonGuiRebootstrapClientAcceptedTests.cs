namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Daemon.DaemonGuiRebootstrapClientTestSupport;

public sealed class DaemonGuiRebootstrapClientAcceptedTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenManifestMatchesAndSupervisorAccepts_ReturnsAccepted ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenManifestMatchesAndSupervisorAccepts_ReturnsAccepted));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, "fingerprint");
        var manifest = CreateManifest();
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var transportClient = CreateAcceptingTransport(unityProject.ProjectFingerprint, manifest);
        var client = CreateClient(transportClient);

        var timeout = TimeSpan.FromMilliseconds(500);
        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            timeout,
            CancellationToken.None);

        Assert.True(result.IsAccepted);
        DaemonGuiRebootstrapTransportAssert.RebootstrapRequestedForManifest(
            transportClient,
            manifest,
            unityProject.ProjectFingerprint,
            timeout);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenManifestStartTimeDiffersWithinTolerance_RequestsSupervisor ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenManifestStartTimeDiffersWithinTolerance_RequestsSupervisor));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, "fingerprint");
        var manifest = CreateManifest() with
        {
            ProcessStartedAtUtc = ProcessStartedAtUtc.AddMilliseconds(1),
        };
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var timeout = TimeSpan.FromMilliseconds(500);
        var transportClient = CreateAcceptingTransport(unityProject.ProjectFingerprint, manifest);
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            timeout,
            CancellationToken.None);

        Assert.True(result.IsAccepted);
        DaemonGuiRebootstrapTransportAssert.RebootstrapRequestedForManifest(
            transportClient,
            manifest,
            unityProject.ProjectFingerprint,
            timeout);
    }

    private static StubIpcTransportClient CreateAcceptingTransport (
        string projectFingerprint,
        GuiSupervisorManifestJsonContract manifest)
    {
        return new StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) => ValueTask.FromResult(IpcResponseTestFactory.CreateSuccess(
                request,
                new IpcGuiRebootstrapResponse(
                    Accepted: true,
                    ProjectFingerprint: projectFingerprint,
                    ProcessId: manifest.ProcessId))),
        };
    }
}
