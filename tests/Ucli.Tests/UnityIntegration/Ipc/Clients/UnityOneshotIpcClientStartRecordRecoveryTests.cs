using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientStartRecordRecoveryTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartResponseIsLost_RetainsAuthoritativeStartAndRunningProcess ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "start-record-response-loss");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var executionTimeout = TimeSpan.FromSeconds(5);
        var completionTimeout =
            executionTimeout
            + LifecycleExecutionTiming.ResponseDeliveryGrace;
        var processHandle =
            StubUnityBatchmodeProcessHandle.CreateNonExiting();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        LifecycleExecutionStartBinding? persistedStart = null;
        var startPersisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingUnityIpcTransportClient(
            async (request, _) =>
            {
                switch (IpcRequestAssert.ParseMethod(request))
                {
                    case UnityIpcMethod.Ping when ++pingAttempt == 1:
                        return CreatePingResponse(request.RequestId);
                    case UnityIpcMethod.Ping:
                        throw new TimeoutException(
                            "Successor oneshot endpoint remained unavailable.");
                    case UnityIpcMethod.LifecycleStart:
                        persistedStart =
                            await LifecycleExecutionIpcTestResponseFactory
                                .PersistStartAsync(unityProject, request);
                        startPersisted.TrySetResult();
                        throw new IpcResponseReadInterruptedException(
                            new EndOfStreamException(
                                "Lifecycle Start response was lost."));
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
        var dispatchRequest = CreateCompileDispatchRequest(
            timeProvider,
            executionTimeout);

        var sendTask = client.SendAsync(
                unityProject,
                dispatchRequest,
                ExecutionDeadline.Start(
                    executionTimeout,
                    timeProvider),
                CancellationToken.None)
            .AsTask();
        await startPersisted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(completionTimeout);
        var result = await sendTask;

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ExecutionErrorCodes.IpcTimeout,
            result.ErrorCode);
        Assert.NotNull(persistedStart);
        Assert.Equal(
            persistedStart!.LifecycleExecutionRef,
            result.LifecycleExecutionStart!.LifecycleExecutionRef);
        Assert.Equal(
            dispatchRequest.Registration!.ExecutionId,
            result.LifecycleExecutionStart.LifecycleExecutionRef.Id);
        Assert.False(result.LifecycleActionDispatched);
        Assert.DoesNotContain(
            transportClient.Requests,
            request => IpcRequestAssert.ParseMethod(request)
                == UnityIpcMethod.Compile);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
        Assert.Equal(0, processHandle.DisposeCount);
    }

}
