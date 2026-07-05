using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorProjectGatewayStopTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryStopProject_WhenManifestIsMalformed_DeletesManifestAndReturnsNull ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "malformed-manifest");
        var timeProvider = new ManualTimeProvider();
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, "{ malformed json", CancellationToken.None);

        var manifestStore = new SupervisorManifestStore();
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Transport should not be used when manifest read fails."),
        };
        var client = new SupervisorClient(transportClient);
        var gateway = SupervisorProjectGatewayTestSupport.CreateGateway(
            manifestStore,
            client,
            new RecordingSupervisorProcessLauncher(),
            timeProvider);

        var result = await gateway.TryStopProjectAsync(
            SupervisorProjectGatewayTestSupport.CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(600),
            CancellationToken.None);

        Assert.Null(result);
        Assert.False(File.Exists(manifestPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryStopProject_WhenProbeConsumesBudget_PassesRemainingTimeoutToStopProject ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "stop-project-timeout");
        var timeProvider = new ManualTimeProvider();
        var scenario = await SupervisorProjectGatewayTestSupport.CreateManifestBackedScenarioAsync(
            scope.FullPath,
            timeProvider);
        var requestedTimeout = TimeSpan.FromMilliseconds(850);
        var observedStopTimeout = TimeSpan.Zero;
        scenario.TransportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(request.Method, SupervisorIpcContracts.PingMethod, StringComparison.Ordinal))
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(220));
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest));
            }

            if (string.Equals(request.Method, SupervisorIpcContracts.StopProjectMethod, StringComparison.Ordinal))
            {
                var payload = SupervisorProjectGatewayTestSupport.ReadStopProjectRequest(request);
                observedStopTimeout = TimeSpan.FromMilliseconds(payload.TimeoutMilliseconds);
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateStopProjectStoppedResponse(request));
            }

            throw new InvalidOperationException($"Unexpected method: {request.Method}");
        };

        var result = await scenario.Gateway.TryStopProjectAsync(
            scenario.CreateUnityProject(),
            requestedTimeout,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.True(observedStopTimeout > TimeSpan.Zero);
        Assert.True(observedStopTimeout < requestedTimeout);
    }
}
