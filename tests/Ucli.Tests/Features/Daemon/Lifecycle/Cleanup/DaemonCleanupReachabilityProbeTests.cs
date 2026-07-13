using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonCleanupReachabilityProbeTests
{
    public static TheoryData<UcliCode> CorrelatedErrorResponseCodes => new()
    {
        IpcSessionErrorCodes.SessionTokenRequired,
        IpcSessionErrorCodes.SessionTokenInvalid,
        IpcProtocolErrorCodes.IpcMethodNotSupported,
    };

    [Theory]
    [MemberData(nameof(CorrelatedErrorResponseCodes))]
    [Trait("Size", "Small")]
    public async Task ProbeWithoutSessionToken_WhenEndpointReturnsCorrelatedError_ReturnsRunning (UcliCode errorCode)
    {
        var unityProject = CreateFingerprintMatchedProject();
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(
            request,
            IpcProtocol.StatusError,
            [
                new IpcError(
                    errorCode,
                    "The endpoint returned a regular IPC error response.",
                    OpId: null),
            ]));
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider(
            "Cleanup endpoint probing without a session token must not resolve session metadata.");
        var pingClient = new IpcDaemonPingClient(
            transportClient,
            sessionConnectionProvider,
            TimeProvider.System);
        var probe = new DaemonCleanupReachabilityProbe(pingClient);

        var result = await probe.ProbeWithoutSessionTokenAsync(
            unityProject,
            ExecutionDeadline.Start(DefaultTimeout, TimeProvider.System),
            CancellationToken.None);

        Assert.Equal(DaemonCleanupReachabilityStatus.Running, result.Status);
        var expectedEndpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        Assert.Equal(expectedEndpoint, Assert.Single(transportClient.Endpoints));
        Assert.Empty(Assert.Single(transportClient.Requests).SessionToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeWithSessionToken_WhenSessionMetadataCannotBeResolved_UsesCanonicalEndpointAndProvidedToken ()
    {
        var unityProject = CreateFingerprintMatchedProject();
        var transportClient = CreateSuccessfulPingTransportClient();
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider(
            "Cleanup endpoint probing must not depend on readable session metadata.");
        var pingClient = new IpcDaemonPingClient(
            transportClient,
            sessionConnectionProvider,
            TimeProvider.System);
        var probe = new DaemonCleanupReachabilityProbe(pingClient);

        var result = await probe.ProbeWithSessionTokenAsync(
            unityProject,
            ExecutionDeadline.Start(DefaultTimeout, TimeProvider.System),
            "metadata-unavailable-probe-token",
            CancellationToken.None);

        Assert.Equal(DaemonCleanupReachabilityStatus.Running, result.Status);
        var expectedEndpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        Assert.Equal(expectedEndpoint, Assert.Single(transportClient.Endpoints));
        Assert.Equal(
            "metadata-unavailable-probe-token",
            Assert.Single(transportClient.Requests).SessionToken);
    }
}
