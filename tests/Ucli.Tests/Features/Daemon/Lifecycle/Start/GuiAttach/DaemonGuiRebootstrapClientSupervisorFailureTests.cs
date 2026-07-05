namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Daemon.DaemonGuiRebootstrapClientTestSupport;

public sealed class DaemonGuiRebootstrapClientSupervisorFailureTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenSupervisorIsUnreachable_ReturnsUnavailable ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenSupervisorIsUnreachable_ReturnsUnavailable));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, "fingerprint");
        var manifest = CreateManifest();
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var timeout = TimeSpan.FromMilliseconds(500);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, _, _, _) => throw new IpcConnectTimeoutException("connect timed out"),
        };
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            timeout,
            CancellationToken.None);

        AssertUnavailableAfterIpc(result, transportClient, manifest, unityProject.ProjectFingerprint, timeout);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenSupervisorReturnsInvalidPayload_ReturnsUnavailable ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenSupervisorReturnsInvalidPayload_ReturnsUnavailable));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, "fingerprint");
        var manifest = CreateManifest();
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var timeout = TimeSpan.FromMilliseconds(500);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) => ValueTask.FromResult(IpcResponseTestFactory.CreateSuccess(
                request,
                new IpcGuiRebootstrapResponse(
                    Accepted: true,
                    ProjectFingerprint: "other-fingerprint",
                    ProcessId: manifest.ProcessId))),
        };
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            timeout,
            CancellationToken.None);

        AssertUnavailableAfterIpc(result, transportClient, manifest, unityProject.ProjectFingerprint, timeout);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenSupervisorReturnsError_ReturnsUnavailable ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenSupervisorReturnsError_ReturnsUnavailable));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, "fingerprint");
        var manifest = CreateManifest();
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var timeout = TimeSpan.FromMilliseconds(500);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) => ValueTask.FromResult(IpcResponseTestFactory.CreateError(
                request,
                UcliCoreErrorCodes.InternalError,
                "rebootstrap failed")),
        };
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            timeout,
            CancellationToken.None);

        AssertUnavailableAfterIpc(result, transportClient, manifest, unityProject.ProjectFingerprint, timeout);
    }

    private static void AssertUnavailableAfterIpc (
        DaemonGuiRebootstrapRequestResult result,
        StubIpcTransportClient transportClient,
        GuiSupervisorManifestJsonContract manifest,
        string projectFingerprint,
        TimeSpan timeout)
    {
        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        DaemonGuiRebootstrapTransportAssert.RebootstrapRequestedForManifest(
            transportClient,
            manifest,
            projectFingerprint,
            timeout);
    }
}
