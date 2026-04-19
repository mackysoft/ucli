using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Runtime;
using MackySoft.Ucli.Features.Daemon.Services;
using MackySoft.Ucli.Features.Daemon.Supervisor;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonStopServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSupervisorIsUnavailable_FallsBackToDirectStopOperation ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-fallback-not-running");
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 3456,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called."),
        };
        var service = CreateService(resolver, transportClient, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStopExecutionOutput>(result.Output);
        Assert.Equal("notRunning", output.StopStatus);
        Assert.Equal("notRunning", output.DaemonStatus);
        Assert.Equal(3456, output.TimeoutMilliseconds);
        Assert.Null(output.Session);
        Assert.Equal(1, daemonStopOperation.StopCallCount);
        Assert.Empty(transportClient.Calls);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenExecutionContextResolutionFails_ReturnsFailureWithoutStopCall ()
    {
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation();
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called."),
        };
        var service = CreateService(resolver, transportClient, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, daemonStopOperation.StopCallCount);
        Assert.Empty(transportClient.Calls);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenDirectStopOperationFailsWithoutError_ReturnsFallbackInternalError ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-fallback-failure");
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1100,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = new DaemonStopResult(DaemonStopStatus.Failed, null),
        };
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called."),
        };
        var service = CreateService(resolver, transportClient, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Daemon stop operation failed without structured error details.", error.Message);
        Assert.Equal(1, daemonStopOperation.StopCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSupervisorManifestPathIsInvalid_ReturnsFailureWithoutDirectFallback ()
    {
        var invalidRepositoryRoot = "/tmp/ucli-invalid-\0-path";
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 2100,
            repositoryRoot: invalidRepositoryRoot);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called."),
        };
        var service = CreateService(resolver, transportClient, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, daemonStopOperation.StopCallCount);
        Assert.Empty(transportClient.Calls);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSupervisorManifestReadExceedsRemainingTimeout_ReturnsTimeoutWithoutDirectFallback ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-manifest-read-timeout");
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 120,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var observedCancellation = false;
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: async (path, cancellationToken) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    return null;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    observedCancellation = true;
                    throw;
                }
            },
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called."),
        };
        var service = CreateService(resolver, transportClient, daemonStopOperation, manifestStore);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(0, daemonStopOperation.StopCallCount);
        Assert.True(observedCancellation);
        Assert.Empty(transportClient.Calls);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSupervisorIsReachable_UsesSupervisorStopProject ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-supervisor");
        var manifest = DaemonServiceTestContext.CreateSupervisorManifest(scope.FullPath);
        await DaemonServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);

        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 2100,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, _, _) =>
            {
                Assert.Equal(manifest.EndpointAddress, endpoint.Address);
                if (request.Method == SupervisorIpcContracts.PingMethod)
                {
                    return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                        request,
                        new SupervisorIpcContracts.PingResponse(manifest.ProcessId, manifest.IssuedAtUtc)));
                }

                if (request.Method == SupervisorIpcContracts.StopProjectMethod)
                {
                    return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                        request,
                        new SupervisorIpcContracts.StopProjectResponse(
                            StopStatus: DaemonStopStateCodec.Stopped,
                            DaemonStatus: DaemonStatusStateCodec.NotRunning)));
                }

                throw new InvalidOperationException($"Unexpected supervisor IPC method: {request.Method}");
            },
        };
        var service = CreateService(resolver, transportClient, daemonStopOperation);

        var result = await service.Stop(
            projectPath: "/tmp/sandbox-unity",
            timeout: "8888",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStopExecutionOutput>(result.Output);
        Assert.Equal("stopped", output.StopStatus);
        Assert.Equal("notRunning", output.DaemonStatus);
        Assert.Equal(0, daemonStopOperation.StopCallCount);

        var stopCall = Assert.Single(
            transportClient.Calls,
            static x => x.Request.Method == SupervisorIpcContracts.StopProjectMethod);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            stopCall.Request.Payload,
            out SupervisorIpcContracts.StopProjectRequest payload,
            out _));
        Assert.Equal(context.Context.UnityProject.UnityProjectRoot, payload.UnityProjectRoot);
        Assert.Equal(context.Context.UnityProject.ProjectFingerprint, payload.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenProbeConsumesBudget_PropagatesRemainingTimeoutToStopProject ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-remaining-timeout");
        var manifest = DaemonServiceTestContext.CreateSupervisorManifest(scope.FullPath);
        await DaemonServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);
        var timeProvider = new ManualTimeProvider();

        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 700,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, _, cancellationToken) =>
            {
                Assert.Equal(manifest.EndpointAddress, endpoint.Address);
                if (request.Method == SupervisorIpcContracts.PingMethod)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                    return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                        request,
                        new SupervisorIpcContracts.PingResponse(manifest.ProcessId, manifest.IssuedAtUtc)));
                }

                if (request.Method == SupervisorIpcContracts.StopProjectMethod)
                {
                    return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                        request,
                        new SupervisorIpcContracts.StopProjectResponse(
                            StopStatus: DaemonStopStateCodec.Stopped,
                            DaemonStatus: DaemonStatusStateCodec.NotRunning)));
                }

                throw new InvalidOperationException($"Unexpected supervisor IPC method: {request.Method}");
            },
        };
        var service = CreateService(
            resolver,
            transportClient,
            daemonStopOperation,
            timeProvider: timeProvider);

        var result = await service.Stop(
            projectPath: "/tmp/sandbox-unity",
            timeout: "700",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, daemonStopOperation.StopCallCount);

        var stopCall = Assert.Single(
            transportClient.Calls,
            static x => x.Request.Method == SupervisorIpcContracts.StopProjectMethod);
        Assert.Equal(TimeSpan.FromMilliseconds(500), stopCall.Timeout);
        Assert.True(stopCall.Timeout < context.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenPingTimesOut_StillUsesSupervisorStopProject ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-ping-timeout");
        var manifest = DaemonServiceTestContext.CreateSupervisorManifest(scope.FullPath);
        await DaemonServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);

        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 2400,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var pingAttemptCount = 0;
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, _, _) =>
            {
                Assert.Equal(manifest.EndpointAddress, endpoint.Address);
                if (request.Method == SupervisorIpcContracts.PingMethod)
                {
                    pingAttemptCount++;
                    throw new TimeoutException("Supervisor ping timed out.");
                }

                if (request.Method == SupervisorIpcContracts.StopProjectMethod)
                {
                    return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                        request,
                        new SupervisorIpcContracts.StopProjectResponse(
                            StopStatus: DaemonStopStateCodec.Stopped,
                            DaemonStatus: DaemonStatusStateCodec.NotRunning)));
                }

                throw new InvalidOperationException($"Unexpected supervisor IPC method: {request.Method}");
            },
        };
        var service = CreateService(resolver, transportClient, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, pingAttemptCount);
        Assert.Equal(0, daemonStopOperation.StopCallCount);
        Assert.Single(
            transportClient.Calls,
            static x => x.Request.Method == SupervisorIpcContracts.StopProjectMethod);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenNamedPipeProbeConnectTimesOutAndSupervisorProcessIsDead_FallsBackToDirectStop ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-named-pipe-stale-manifest");
        var manifest = new SupervisorInstanceManifest(
            ProcessId: int.MaxValue,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
        await DaemonServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);

        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 2400,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var service = CreateService(resolver, transportClient, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, daemonStopOperation.StopCallCount);
        Assert.Single(
            transportClient.Calls,
            static x => x.Request.Method == SupervisorIpcContracts.PingMethod);
    }

    private static DaemonStopService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        DaemonServiceTestContext.StubIpcTransportClient transportClient,
        IDaemonStopOperation daemonStopOperation,
        SupervisorManifestStore? manifestStore = null,
        TimeProvider? timeProvider = null)
    {
        return new DaemonStopService(
            resolver,
            manifestStore ?? new SupervisorManifestStore(),
            DaemonServiceTestContext.CreateSupervisorClient(transportClient),
            daemonStopOperation,
            timeProvider);
    }
}