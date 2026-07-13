using System.Text;
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
    public async Task TryStopProject_WhenTokenRotatesBeforePing_ReloadsAndProbesSuccessorGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-project-gateway",
            "ping-token-rotation");
        var scenario = await SupervisorProjectGatewayTestSupport.CreateManifestBackedScenarioAsync(scope.FullPath);
        var successorManifest = SupervisorClientTestSupport.CreateSuccessorManifest(
            scenario.Manifest,
            sessionTokenDiscriminator: 2);
        var pingAttempt = 0;
        scenario.TransportClient.SendHandler = async (_, request, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(request.Method, ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping), StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref pingAttempt) == 1)
                {
                    await scenario.ManifestStore.WriteAsync(
                        scope.FullPath,
                        successorManifest,
                        cancellationToken);
                    return IpcResponseTestFactory.CreateError(
                        request,
                        IpcSessionErrorCodes.SessionTokenInvalid,
                        "Initial supervisor token is invalid.");
                }

                return SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    successorManifest);
            }

            Assert.Equal(ContractLiteralCodec.ToValue(SupervisorIpcMethod.StopProject), request.Method);
            Assert.Equal(successorManifest.SessionToken.GetEncodedValue(), request.SessionToken);
            return SupervisorProjectGatewayTestSupport.CreateStopProjectStoppedResponse(request);
        };

        var result = await scenario.Gateway.TryStopProjectAsync(
            scenario.CreateUnityProject(),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Collection(
            scenario.TransportClient.Invocations,
            invocation => Assert.Equal(scenario.Manifest.SessionToken.GetEncodedValue(), invocation.Request.SessionToken),
            invocation => Assert.Equal(successorManifest.SessionToken.GetEncodedValue(), invocation.Request.SessionToken),
            invocation => Assert.Equal(successorManifest.SessionToken.GetEncodedValue(), invocation.Request.SessionToken));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryStopProject_WhenTokenRotatesAfterPing_ReloadsManifestAndReplaysSameStopRequestOnce ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-project-gateway",
            "stop-project-token-rotation");
        var timeProvider = new ManualTimeProvider();
        var scenario = await SupervisorProjectGatewayTestSupport.CreateManifestBackedScenarioAsync(
            scope.FullPath,
            timeProvider);
        var successorManifest = SupervisorClientTestSupport.CreateSuccessorManifest(
            scenario.Manifest,
            sessionTokenDiscriminator: 2);
        var stopAttempt = 0;
        scenario.TransportClient.SendHandler = async (_, request, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(request.Method, ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping), StringComparison.Ordinal))
            {
                return SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest);
            }

            Assert.Equal(ContractLiteralCodec.ToValue(SupervisorIpcMethod.StopProject), request.Method);
            if (Interlocked.Increment(ref stopAttempt) == 1)
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                await scenario.ManifestStore.WriteAsync(
                    scope.FullPath,
                    successorManifest,
                    cancellationToken);
                return IpcResponseTestFactory.CreateError(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "Initial supervisor token is invalid.");
            }

            return SupervisorProjectGatewayTestSupport.CreateStopProjectStoppedResponse(request);
        };

        var result = await scenario.Gateway.TryStopProjectAsync(
            scenario.CreateUnityProject(),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        var requests = scenario.TransportClient.Invocations
            .Select(static invocation => invocation.Request)
            .Where(static request => request.Method == ContractLiteralCodec.ToValue(SupervisorIpcMethod.StopProject))
            .ToArray();
        IpcRequestAssert.SessionTokens(
            requests,
            scenario.Manifest.SessionToken.GetEncodedValue(),
            successorManifest.SessionToken.GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
        var firstPayload = SupervisorProjectGatewayTestSupport.ReadStopProjectRequest(requests[0]);
        var replayPayload = SupervisorProjectGatewayTestSupport.ReadStopProjectRequest(requests[1]);
        Assert.Equal(firstPayload.DeadlineUtc, replayPayload.DeadlineUtc);
        Assert.True(firstPayload.AttemptTimeoutMilliseconds > replayPayload.AttemptTimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryStopProject_WhenManifestIsMalformed_DeletesManifestAndReturnsNull ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "malformed-manifest");
        var timeProvider = new ManualTimeProvider();
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, "{ malformed json", CancellationToken.None);

        var manifestStore = SupervisorManifestStoreTestSupport.CreateFileBacked(timeProvider);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Transport should not be used when manifest read fails."),
        };
        var client = new SupervisorClient(transportClient, timeProvider);
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
    public async Task TryStopProject_WhenMalformedManifestIsReplacedDuringCleanup_RetriesOnceWithSuccessorGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "malformed-manifest-replaced");
        var timeProvider = new ManualTimeProvider();
        var originalManifest = SupervisorClientTestSupport.CreateManifest();
        var successorManifest = SupervisorClientTestSupport.CreateSuccessorManifest(
            originalManifest,
            sessionTokenDiscriminator: 2);
        var manifestReadCount = 0;
        var manifestStore = new SupervisorManifestStore(
            timeProvider,
            (_, _) => ValueTask.FromResult<ReadOnlyMemory<byte>?>(Encoding.UTF8.GetBytes(
                Interlocked.Increment(ref manifestReadCount) == 1
                    ? "{ malformed json"
                    : SupervisorManifestStoreTestSupport.Serialize(successorManifest))),
            static (_, _, _) => ValueTask.CompletedTask,
            static _ => throw new InvalidOperationException("A successor manifest must not be deleted."));
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.Equal(successorManifest.SessionToken.GetEncodedValue(), request.SessionToken);
                return string.Equals(request.Method, ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping), StringComparison.Ordinal)
                    ? ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                        request,
                        successorManifest))
                    : ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateStopProjectStoppedResponse(request));
            },
        };
        var client = new SupervisorClient(transportClient, timeProvider);
        var gateway = SupervisorProjectGatewayTestSupport.CreateGateway(
            manifestStore,
            client,
            new RecordingSupervisorProcessLauncher(),
            timeProvider);

        var result = await gateway.TryStopProjectAsync(
            SupervisorProjectGatewayTestSupport.CreateUnityProject(scope.FullPath),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Collection(
            transportClient.Invocations,
            invocation => Assert.Equal(ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping), invocation.Request.Method),
            invocation => Assert.Equal(ContractLiteralCodec.ToValue(SupervisorIpcMethod.StopProject), invocation.Request.Method));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryStopProject_WhenManifestChangesAgainDuringSuccessorRetry_ReturnsWithoutThirdObservation ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "manifest-changes-twice");
        var timeProvider = new ManualTimeProvider();
        var firstSuccessor = SupervisorClientTestSupport.CreateManifest(sessionTokenDiscriminator: 2);
        var secondSuccessor = SupervisorClientTestSupport.CreateSuccessorManifest(
            firstSuccessor,
            sessionTokenDiscriminator: 3);
        var manifestReadCount = 0;
        var manifestStore = new SupervisorManifestStore(
            timeProvider,
            (_, _) => ValueTask.FromResult<ReadOnlyMemory<byte>?>(Encoding.UTF8.GetBytes(
                Interlocked.Increment(ref manifestReadCount) switch
                {
                    1 => "{ malformed generation one",
                    2 => SupervisorManifestStoreTestSupport.Serialize(firstSuccessor),
                    3 => "{ malformed generation two",
                    _ => SupervisorManifestStoreTestSupport.Serialize(secondSuccessor),
                })),
            static (_, _, _) => ValueTask.CompletedTask,
            static _ => throw new InvalidOperationException("A successor manifest must not be deleted."));
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException(
                "A third manifest observation must not dispatch supervisor IPC."),
        };
        var gateway = SupervisorProjectGatewayTestSupport.CreateGateway(
            manifestStore,
            new SupervisorClient(transportClient, timeProvider),
            new RecordingSupervisorProcessLauncher(),
            timeProvider);

        var result = await gateway.TryStopProjectAsync(
            SupervisorProjectGatewayTestSupport.CreateUnityProject(scope.FullPath),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(4, manifestReadCount);
        Assert.Empty(transportClient.Invocations);
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

            if (string.Equals(request.Method, ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping), StringComparison.Ordinal))
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(220));
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest));
            }

            if (string.Equals(request.Method, ContractLiteralCodec.ToValue(SupervisorIpcMethod.StopProject), StringComparison.Ordinal))
            {
                var payload = SupervisorProjectGatewayTestSupport.ReadStopProjectRequest(request);
                observedStopTimeout = TimeSpan.FromMilliseconds(payload.AttemptTimeoutMilliseconds);
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
