using System.Text.Json;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientLaunchTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenSuccessful_AcquiresProjectLockAndUsesConfiguredEndpoint ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "success");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var lockProvider = new StubProjectLifecycleLockProvider();
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var client = CreateClient(
            launcher,
            transportClient,
            lockProvider,
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        ProjectLifecycleLockProviderAssert.AcquiredOnceFor(lockProvider, unityProject);
        var bootstrapArguments = UnityOneshotLaunchAssert.LaunchedOnceWithDefaultOptions(launcher, unityProject);
        var requests = IpcRequestAssert.Methods(transportClient, UnityIpcMethod.Ping, UnityIpcMethod.OpsRead);
        var dispatchRequest = IpcRequestAssert.SingleWithMethod(requests, UnityIpcMethod.OpsRead);
        Assert.Equal(CreateDispatchPayload().GetRawText(), dispatchRequest.Payload.GetRawText());
        Assert.All(transportClient.UnityInvocations, invocation =>
        {
            Assert.Equal(startedAtUtc + TimeSpan.FromSeconds(30), invocation.Request.RequestDeadlineUtc);
        });
        IpcRequestAssert.AllSessionToken(requests, bootstrapArguments.SessionToken.GetEncodedValue());
        UnityBatchmodeProcessHandleAssert.WaitedForExitWithoutTermination(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WithNewLifecycleExecution_UsesGraceOnlyAfterStartWrite ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "lifecycle-deadlines");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.Compile => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var result = await client.SendAsync(
            unityProject,
            CreateCompileDispatchRequest(
                timeProvider,
                TimeSpan.FromSeconds(30)),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var bootstrap = UnityOneshotLaunchAssert.LaunchedOnce(launcher, unityProject);
        Assert.Equal(startedAtUtc + TimeSpan.FromSeconds(33), bootstrap.ExitDeadlineUtc);
        var startupPing = IpcRequestAssert.SingleWithMethod(
            transportClient,
            UnityIpcMethod.Ping);
        Assert.Equal(
            startedAtUtc + TimeSpan.FromSeconds(30),
            startupPing.RequestDeadlineUtc);
        var lifecycleStartRequest = IpcRequestAssert.SingleWithMethod(
            transportClient,
            UnityIpcMethod.LifecycleStart);
        Assert.Equal(
            startedAtUtc + TimeSpan.FromSeconds(33),
            lifecycleStartRequest.RequestDeadlineUtc);
        var actionRequest = IpcRequestAssert.SingleWithMethod(
            transportClient,
            UnityIpcMethod.Compile);
        Assert.Equal(
            startedAtUtc + TimeSpan.FromSeconds(33),
            actionRequest.RequestDeadlineUtc);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            lifecycleStartRequest.Payload,
            out IpcLifecycleExecutionStartRequest start,
            out var readError),
            readError.Message);
        Assert.Equal(startedAtUtc + TimeSpan.FromSeconds(30), start.DeadlineUtc);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenLifecycleResponseRemainsPublishing_PreservesOwningOneshotProcess ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "lifecycle-publishing-response");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(processHandle));
        LifecycleExecutionStartBinding? persistedStart = null;
        var transportClient = new RecordingUnityIpcTransportClient(
            async (request, _) =>
            {
                switch (IpcRequestAssert.ParseMethod(request))
                {
                    case UnityIpcMethod.Ping:
                        return CreatePingResponse(request.RequestId);
                    case UnityIpcMethod.LifecycleStart:
                        persistedStart =
                            await LifecycleExecutionIpcTestResponseFactory
                                .PersistStartAsync(unityProject, request);
                        return LifecycleExecutionIpcTestResponseFactory
                            .CreateResponse(request, persistedStart);
                    case UnityIpcMethod.Compile:
                    {
                        Assert.NotNull(persistedStart);
                        var store =
                            FileLifecycleExecutionStore.CreateForProject(
                                unityProject.UnityProjectRoot,
                                unityProject.ProjectFingerprint);
                        var stored = await store.ReadAsync(
                            LifecycleExecutionKind.Compile,
                            persistedStart!.LifecycleExecutionRef.Id,
                            CancellationToken.None);
                        Assert.NotNull(stored);
                        var publishingReference =
                            LifecycleExecutionReferenceFactory
                                .CreateStateProjection(
                                    stored!.CurrentReference,
                                    ExecutionLifecycle.Recovery,
                                    LifecycleExecutionState.Publishing);
                        var publishingStart =
                            new LifecycleExecutionStartBinding(
                                publishingReference,
                                stored.Start.Project,
                                stored.Start.Host,
                                stored.Start.StartedGeneration,
                                stored.Start.DeadlineUtc,
                                stored.Start.StartedAtUtc);
                        var terminalRecord =
                            new CompileLifecycleExecutionTerminalRecord(
                                publishingReference.Id,
                                publishingReference.DefinitionDigest,
                                publishingStart.Project,
                                publishingStart.Host,
                                publishingStart.StartedGeneration,
                                terminalGeneration: null,
                                publishingStart.DeadlineUtc,
                                publishingStart.StartedAtUtc,
                                publishingStart.DeadlineUtc,
                                LifecycleExecutionTerminalReason
                                    .DeadlineExceeded,
                                ExecutionApplicationState.Indeterminate,
                                result: null,
                                verdict: null,
                                artifactRefs: Array.Empty<ArtifactRef>());
                        var interruptedRecord =
                            new LifecycleExecutionStoreRecord(
                                LifecycleExecutionStoreRecord
                                    .CurrentSchemaVersion,
                                publishingStart,
                                terminalReference: null,
                                new LifecycleExecutionTerminalPublicationIntent(
                                    publishingStart.Host
                                        .CurrentEndpointRegistrationGenerationId,
                                    JsonSerializer
                                        .SerializeToUtf8Bytes
                                            <LifecycleExecutionTerminalRecord>(
                                                terminalRecord,
                                                IpcJsonSerializerOptions
                                                    .Default)),
                                sideEffectRightOwnerEndpointRegistrationGenerationId:
                                    null,
                                new[]
                                {
                                    publishingStart.Host
                                        .FirstEndpointRegistrationGenerationId,
                                });
                        await File.WriteAllTextAsync(
                            store.Paths.ResolveRecordPath(
                                    LifecycleExecutionKind.Compile,
                                    publishingReference.Id)
                                .Value,
                            JsonSerializer.Serialize(
                                    interruptedRecord,
                                    IpcJsonSerializerOptions.Default)
                                + Environment.NewLine,
                            CancellationToken.None);
                        return new IpcResponse(
                            IpcProtocol.CurrentVersion,
                            request.RequestId,
                            IpcResponseStatus.Error,
                            IpcPayloadCodec.SerializeToElement(
                                new IpcCompileErrorResponse(
                                    publishingReference,
                                    ExecutionApplicationState.Indeterminate,
                                    result: null,
                                    observedLifecycle: null)),
                            [
                                new IpcError(
                                    LifecycleExecutionErrorCodes
                                        .TerminalPublicationFailed,
                                    "Terminal publication remains recoverable.",
                                    null),
                            ]);
                    }
                    default:
                        throw new Xunit.Sdk.XunitException(
                            $"Unexpected method: {request.Method}");
                }
            },
            createLifecycleStartResponses: false);
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateCompileDispatchRequest(),
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.LifecycleExecutionStart);
        Assert.True(result.LifecycleActionDispatched);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
        Assert.Equal(0, processHandle.DisposeCount);
        Assert.Single(processHandle.WaitForExitInvocations);
        Assert.DoesNotContain(
            transportClient.Requests,
            request => IpcRequestAssert.ParseMethod(request)
                == UnityIpcMethod.Shutdown);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenCallerCancelsAfterLifecycleStart_KeepsOneshotActionRunning ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "lifecycle-caller-cancellation");
        using var callerCancellation = new CancellationTokenSource();
        var actionResponseSource =
            new TaskCompletionSource<IpcResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var transportClient = new RecordingUnityIpcTransportClient(
            (request, requestCancellationToken) =>
            {
                return IpcRequestAssert.ParseMethod(request) switch
                {
                    UnityIpcMethod.Ping =>
                        ValueTask.FromResult(CreatePingResponse(request.RequestId)),
                    UnityIpcMethod.Compile => AwaitCompileResponse(),
                    _ => throw new Xunit.Sdk.XunitException(
                        $"Unexpected method: {request.Method}"),
                };

                ValueTask<IpcResponse> AwaitCompileResponse ()
                {
                    Assert.False(requestCancellationToken.CanBeCanceled);
                    callerCancellation.Cancel();
                    return new ValueTask<IpcResponse>(actionResponseSource.Task);
                }
            });
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateCompileDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            callerCancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.ErrorCode);
        Assert.NotNull(result.LifecycleExecutionStart);
        Assert.True(result.LifecycleActionDispatched);
        var actionRequest = IpcRequestAssert.SingleWithMethod(
            transportClient,
            UnityIpcMethod.Compile);

        actionResponseSource.TrySetResult(CreateSuccessResponse(actionRequest.RequestId));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenExecutionDeadlineExpiresAfterStartWrite_UsesCompletionGraceForActionDelivery ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "lifecycle-deadline-before-action");
        var timeProvider = new ManualTimeProvider();
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var transportClient = new RecordingUnityIpcTransportClient(
            request =>
            {
                switch (IpcRequestAssert.ParseMethod(request))
                {
                    case UnityIpcMethod.Ping:
                        return CreatePingResponse(request.RequestId);
                    case UnityIpcMethod.LifecycleStart:
                        var response =
                            LifecycleExecutionIpcTestResponseFactory.TryCreateResponse(request)!;
                        timeProvider.Advance(TimeSpan.FromSeconds(31));
                        return response;
                    case UnityIpcMethod.Compile:
                        return CreateSuccessResponse(request.RequestId);
                    default:
                        throw new Xunit.Sdk.XunitException(
                            $"Unexpected method: {request.Method}");
                }
            },
            createLifecycleStartResponses: false);
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);

        var result = await client.SendAsync(
            unityProject,
            CreateCompileDispatchRequest(
                timeProvider,
                TimeSpan.FromSeconds(30)),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.LifecycleExecutionStart);
        Assert.True(result.LifecycleActionDispatched);
        var actionRequest = IpcRequestAssert.SingleWithMethod(
            transportClient,
            UnityIpcMethod.Compile);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(33),
            actionRequest.RequestDeadlineUtc);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UnityIpcMethod.LifecycleStart)]
    [InlineData(UnityIpcMethod.Compile)]
    public async Task SendAsync_WhenLifecycleResponseIsInterrupted_RebindsAndReplaysTheSameLogicalExecution (
        UnityIpcMethod interruptedMethod)
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "lifecycle-response-replay");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var lifecycleStartAttempt = 0;
        var compileAttempt = 0;
        var transportClient = new RecordingUnityIpcTransportClient(
            request =>
            {
                return IpcRequestAssert.ParseMethod(request) switch
                {
                    UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                    UnityIpcMethod.LifecycleStart
                        when ++lifecycleStartAttempt == 1
                        && interruptedMethod == UnityIpcMethod.LifecycleStart =>
                        throw new IpcResponseReadInterruptedException(
                            new EndOfStreamException("start response was interrupted")),
                    UnityIpcMethod.LifecycleStart =>
                        LifecycleExecutionIpcTestResponseFactory.TryCreateResponse(request)!,
                    UnityIpcMethod.Compile
                        when ++compileAttempt == 1
                        && interruptedMethod == UnityIpcMethod.Compile =>
                        throw new IpcResponseReadInterruptedException(
                            new EndOfStreamException("domain reload interrupted the response")),
                    UnityIpcMethod.Compile => CreateSuccessResponse(request.RequestId),
                    _ => throw new Xunit.Sdk.XunitException(
                        $"Unexpected method: {request.Method}"),
                };
            },
            createLifecycleStartResponses: false);
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateCompileDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.LifecycleExecutionStart);
        var startRequests = IpcRequestAssert.WithMethod(
            transportClient,
            UnityIpcMethod.LifecycleStart);
        var actionRequests = IpcRequestAssert.WithMethod(
            transportClient,
            UnityIpcMethod.Compile);
        Assert.Equal(2, startRequests.Count);
        Assert.Equal(
            interruptedMethod == UnityIpcMethod.Compile ? 2 : 1,
            actionRequests.Count);
        _ = IpcRequestAssert.SingleRequestId(startRequests);
        _ = IpcRequestAssert.SingleRequestId(actionRequests);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenReconnectFailsAfterLifecycleStart_PreservesTheRunningOneshotProcess ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "lifecycle-reconnect-failure");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var transportClient = new RecordingUnityIpcTransportClient(
            request =>
            {
                return IpcRequestAssert.ParseMethod(request) switch
                {
                    UnityIpcMethod.Ping when ++pingAttempt == 1 =>
                        CreatePingResponse(request.RequestId),
                    UnityIpcMethod.Ping =>
                        CreateSuccessResponse(request.RequestId),
                    UnityIpcMethod.LifecycleStart =>
                        LifecycleExecutionIpcTestResponseFactory.TryCreateResponse(request)!,
                    UnityIpcMethod.Compile =>
                        throw new IpcResponseReadInterruptedException(
                            new EndOfStreamException("domain reload interrupted the response")),
                    _ => throw new Xunit.Sdk.XunitException(
                        $"Unexpected method: {request.Method}"),
                };
            },
            createLifecycleStartResponses: false);
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateCompileDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.LifecycleExecutionStart);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
        Assert.Equal(0, processHandle.DisposeCount);
        Assert.Single(processHandle.WaitForExitInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenClientFailsAfterLifecycleStart_PreservesTheRunningOneshotProcess ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "lifecycle-client-failure");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = StubUnityBatchmodeProcessHandle.CreateNonExiting();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(
            request =>
            {
                return IpcRequestAssert.ParseMethod(request) switch
                {
                    UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                    UnityIpcMethod.LifecycleStart =>
                        LifecycleExecutionIpcTestResponseFactory.TryCreateResponse(request)!,
                    UnityIpcMethod.Compile =>
                        throw new InvalidOperationException("client response processing failed"),
                    _ => throw new Xunit.Sdk.XunitException(
                        $"Unexpected method: {request.Method}"),
                };
            },
            createLifecycleStartResponses: false);
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            CreateCompileDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.LifecycleExecutionStart);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
        Assert.Equal(0, processHandle.DisposeCount);
        Assert.Single(processHandle.WaitForExitInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WithOneshotActiveBuildProfilePath_PassesLaunchOptions ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "active-build-profile");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreatePingResponse(request.RequestId),
                UnityIpcMethod.BuildRun => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            unityProject,
            new UnityIpcDispatchRequest(
                UnityIpcMethod.BuildRun,
                CreateDispatchPayload(),
                new UnityBatchmodeLaunchOptions(new UnityBuildProfileAssetPath(
                    "Assets/BuildProfiles/LinuxPlayer.asset"))),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        UnityOneshotLaunchAssert.LaunchedOnceWithActiveBuildProfile(
            launcher,
            unityProject,
            "Assets/BuildProfiles/LinuxPlayer.asset");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenLifecycleLockAcquisitionTimesOut_ReturnsIpcTimeoutWithoutLaunchingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "lock-timeout");
        var launcher = new UnexpectedUnityBatchmodeProcessLauncher("Lifecycle lock timeout should not launch Unity.");
        var client = CreateClient(
            launcher,
            new RecordingUnityIpcTransportClient(_ => CreateSuccessResponse(Guid.NewGuid())),
            new StubProjectLifecycleLockProvider((_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Timed out while waiting for lifecycle lock.");
            }),
            CreateProjectLockPreflightService());

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
    }

}
