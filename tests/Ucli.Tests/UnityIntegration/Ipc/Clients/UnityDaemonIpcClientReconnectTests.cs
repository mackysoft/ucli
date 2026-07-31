using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientReconnectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReconnectAsync_WhenFixedProcessGenerationExited_ReturnsExactHostExitWithoutResolvingSession ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-daemon-ipc-client",
            "reconnect-fixed-host-exit");
        var unityProject =
            ResolvedUnityProjectContextTestFactory
                .CreateForRepositoryRoot(scope.FullPath);
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile);
        var liveProcess = ProcessLivenessProbe.CaptureCurrentProcess();
        var exitedGeneration = new ProcessIdentity(
            liveProcess.ProcessId,
            liveProcess.Generation == ulong.MaxValue
                ? liveProcess.Generation - 1
                : liveProcess.Generation + 1);
        var requiredStart = CreateRequiredStart(
            unityProject,
            registration,
            exitedGeneration);
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));
        var transportClient = new RecordingUnityIpcTransportClient(
            _ => throw new Xunit.Sdk.XunitException(
                "A confirmed fixed-host exit must not send an IPC request."));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new UnexpectedDaemonSessionStore(
                    "A confirmed fixed-host exit must not resolve a daemon session.")));

        var attempt = await client.TryReconnectAsync(
            unityProject,
            dispatchRequest,
            requiredStart,
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                TimeProvider.System),
            CancellationToken.None);

        Assert.True(attempt.IsOwned);
        Assert.False(attempt.Result!.IsSuccess);
        Assert.Equal(
            EditorLifecycleErrorCodes.EditorUnavailable,
            attempt.Result.ErrorCode);
        Assert.Equal(
            exitedGeneration,
            attempt.Result.ConfirmedHostExit!.Process);
        Assert.Empty(transportClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReconnectAsync_WhenSameHostPublishesSuccessorGeneration_UsesSuccessorWithoutReselectingHost ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-daemon-ipc-client",
            "reconnect-same-host-successor");
        var unityProject =
            ResolvedUnityProjectContextTestFactory
                .CreateForRepositoryRoot(scope.FullPath);
        var processIdentity =
            ProcessLivenessProbe.CaptureCurrentProcess();
        var processStartedAtUtc = System.Diagnostics.Process
            .GetCurrentProcess()
            .StartTime
            .ToUniversalTime();
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile);
        var requiredStart = CreateRequiredStart(
            unityProject,
            registration,
            processIdentity);
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));
        var firstSession = CreateSession(
            unityProject,
            processIdentity.ProcessId,
            processStartedAtUtc,
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var successorSession = CreateSession(
            unityProject,
            processIdentity.ProcessId,
            processStartedAtUtc,
            "daemon-token-2",
            Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var transportClient = new RecordingUnityIpcTransportClient(
            request =>
            {
                if (request.SessionToken
                    == firstSession.SessionToken.GetEncodedValue())
                {
                    return UnityDaemonIpcClientTestSupport
                        .CreateSessionTokenInvalidResponse();
                }

                return IpcRequestAssert.ParseMethod(request) switch
                {
                    UnityIpcMethod.LifecycleStart =>
                        LifecycleExecutionIpcTestResponseFactory
                            .CreateResponse(request, requiredStart),
                    UnityIpcMethod.Compile =>
                        UnityDaemonIpcClientTestSupport.CreateResponse(
                            request.RequestId),
                    _ => throw new Xunit.Sdk.XunitException(
                        $"Unexpected method: {request.Method}"),
                };
            },
            createLifecycleStartResponses: false);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(
                        firstSession),
                    DaemonSessionReadResultTestFactory.Found(
                        successorSession))));

        var attempt = await client.TryReconnectAsync(
            unityProject,
            dispatchRequest,
            requiredStart,
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                TimeProvider.System),
            CancellationToken.None);

        Assert.True(attempt.IsOwned);
        Assert.True(attempt.Result!.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient.Requests,
            UnityIpcMethod.LifecycleStart,
            UnityIpcMethod.LifecycleStart,
            UnityIpcMethod.Compile);
        IpcRequestAssert.SessionTokens(
            requests,
            firstSession.SessionToken.GetEncodedValue(),
            successorSession.SessionToken.GetEncodedValue(),
            successorSession.SessionToken.GetEncodedValue());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReconnectAsync_WhenCurrentDaemonSessionBelongsToDifferentHost_DoesNotSend ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-daemon-ipc-client",
            "reconnect-different-host");
        var unityProject =
            ResolvedUnityProjectContextTestFactory
                .CreateForRepositoryRoot(scope.FullPath);
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile);
        var requiredStart = CreateRequiredStart(
            unityProject,
            registration,
            ProcessLivenessProbe.CaptureCurrentProcess());
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));
        var differentHostSession = CreateSession(
            unityProject,
            int.MaxValue,
            DateTimeOffset.UtcNow,
            "different-host-token",
            Guid.NewGuid());
        var transportClient = new RecordingUnityIpcTransportClient(
            _ => throw new Xunit.Sdk.XunitException(
                "A mismatched daemon host must not receive a reconnect request."));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(
                        differentHostSession))));

        var attempt = await client.TryReconnectAsync(
            unityProject,
            dispatchRequest,
            requiredStart,
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                TimeProvider.System),
            CancellationToken.None);

        Assert.False(attempt.IsOwned);
        Assert.Empty(transportClient.Requests);
    }

    private static DaemonSession CreateSession (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        string sessionToken,
        Guid sessionGenerationId)
    {
        return DaemonSessionTestFactory.Create(
            processId,
            sessionToken,
            unityProject.ProjectFingerprint,
            editorMode: UnityEditorMode.Batchmode,
            processStartedAtUtc: processStartedAtUtc,
            sessionGenerationId: sessionGenerationId);
    }

    private static LifecycleExecutionStartBinding CreateRequiredStart (
        ResolvedUnityProjectContext unityProject,
        LifecycleExecutionRegistration registration,
        ProcessIdentity processIdentity)
    {
        var definitionDigest =
            LifecycleExecutionDefinitionDigest.Calculate(
                registration.Definition);
        return new LifecycleExecutionStartBinding(
            new ActiveExecutionRef(
                registration.Definition.ExecutionKind,
                registration.ExecutionId,
                definitionDigest,
                new ExecutionState(TextVocabulary.GetText(
                    LifecycleExecutionState.Registered)),
                new ExecutionStatusLocator(
                    $"lifecycle-executions/{registration.ExecutionId:N}/status.json")),
            new UnityProjectIdentity(
                unityProject.UnityProjectRoot.Value,
                unityProject.ProjectFingerprint,
                unityProject.UnityVersion),
            new LifecycleExecutionHostRegistration(
                processIdentity,
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new UnityEditorGenerationSnapshot(10, 20, 30, 40),
            registration.DeadlineUtc,
            registration.StartedAtUtc);
    }
}
