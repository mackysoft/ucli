using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonCleanupReachabilityProbeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenSessionMetadataCannotBeResolved_UsesCanonicalEndpointDirectly ()
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

        var result = await probe.ProbeAsync(
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
