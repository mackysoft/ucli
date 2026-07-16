namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Daemon.DaemonGuiRebootstrapClientTestSupport;

public sealed class DaemonGuiRebootstrapClientMalformedEndpointTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenEndpointAddressViolatesTransportContract_ReturnsUnavailableWithoutIpc ()
    {
        var timeProvider = new ManualTimeProvider();
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenEndpointAddressViolatesTransportContract_ReturnsUnavailableWithoutIpc));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"));
        var manifest = CreateManifest();
        await WriteManifestAsync(
            scope.FullPath,
            unityProject.ProjectFingerprint,
            new
            {
                manifest.SchemaVersion,
                SessionToken = manifest.SessionToken.GetEncodedValue(),
                ProjectFingerprint = manifest.ProjectFingerprint.ToString(),
                EndpointTransportKind = ContractLiteralCodec.ToValue(manifest.Endpoint.TransportKind),
                EndpointAddress = "relative/ucli-gui-supervisor.sock",
                manifest.ProcessId,
                manifest.ProcessStartedAtUtc,
                manifest.IssuedAtUtc,
            });
        var transportClient = new StubIpcTransportClient();
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        DaemonGuiRebootstrapTransportAssert.NoIpcRequestWasSent(transportClient);
    }
}
