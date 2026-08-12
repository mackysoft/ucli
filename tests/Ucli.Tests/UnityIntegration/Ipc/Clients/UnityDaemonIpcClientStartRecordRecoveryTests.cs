using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientStartRecordRecoveryTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartResponseIsLost_RetainsAuthoritativeStartRecord ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-daemon-ipc-client",
            "start-record-response-loss");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var executionTimeout = TimeSpan.FromSeconds(5);
        var completionTimeout =
            executionTimeout
            + LifecycleExecutionTiming.ResponseDeliveryGrace;
        LifecycleExecutionStartBinding? persistedStart = null;
        var startPersisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingIpcTransportClient(
            async (request, _) =>
            {
                Assert.Equal(
                    UnityIpcMethod.LifecycleStart,
                    IpcRequestAssert.ParseMethod(request));
                persistedStart =
                    await LifecycleExecutionIpcTestResponseFactory
                        .PersistStartAsync(unityProject, request);
                startPersisted.TrySetResult();
                throw new IpcResponseReadInterruptedException(
                    new EndOfStreamException(
                        "Lifecycle Start response was lost."));
            },
            createLifecycleStartResponses: false);
        var interruptedSession = DaemonSessionTestFactory.Create(
            processId: 1234,
            sessionToken: "daemon-token",
            projectFingerprint: unityProject.ProjectFingerprint,
            processStartedAtUtc:
                new DateTimeOffset(
                    2026,
                    3,
                    5,
                    0,
                    0,
                    1,
                    TimeSpan.Zero),
            editorMode: UnityEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            editorInstanceId:
                Guid.Parse(
                    "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            sessionGenerationId: Guid.NewGuid());
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(
                        interruptedSession),
                    DaemonSessionReadResult.Missing()),
                CreateRecoveryWaiter(
                    interruptedSession,
                    timeProvider,
                    completionTimeout)));
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.Compile,
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
        Assert.Empty(
            IpcRequestAssert.ActionRequests(
                transportClient.Requests));
    }

}
