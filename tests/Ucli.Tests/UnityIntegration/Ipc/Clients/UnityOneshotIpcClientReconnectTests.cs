using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientReconnectTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryReconnectAsync_WhenFixedProcessGenerationExited_ReturnsExactHostExitWithoutReadingProviderEndpoint ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
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
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new RecordingUnityProjectLockPreflightService());

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
        Assert.Empty(launcher.Invocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryReconnectAsync_WhenExistingEnvelopeProvesRequiredHost_DispatchesWithoutLaunching ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "reconnect-existing-envelope");
        var unityProject =
            ResolvedUnityProjectContextTestFactory
                .CreateForRepositoryRoot(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var timeProvider = new ManualTimeProvider(nowUtc);
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile,
                timeProvider: timeProvider,
                executionTimeout: TimeSpan.FromMinutes(1));
        var requiredStart = CreateRequiredStart(
            unityProject,
            registration,
            ProcessLivenessProbe.CaptureCurrentProcess());
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));
        var sessionToken = IpcSessionToken.CreateRandom();
        OneshotBootstrapEnvelopeStore.Create(
            unityProject.RepositoryRoot,
            new IpcOneshotBootstrapEnvelope(
                Guid.NewGuid(),
                ProcessLivenessProbe.CaptureCurrentProcess(),
                unityProject.ProjectFingerprint,
                sessionToken,
                nowUtc,
                nowUtc.AddMinutes(1),
                UcliIpcEndpointResolver.ResolveDaemonEndpoint(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint).Contract));
        var transportClient = new RecordingUnityIpcTransportClient(
            request => IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.LifecycleStart =>
                    LifecycleExecutionIpcTestResponseFactory.CreateResponse(
                        request,
                        requiredStart),
                UnityIpcMethod.Compile =>
                    CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException(
                    $"Unexpected method: {request.Method}"),
            },
            createLifecycleStartResponses: false);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new RecordingUnityProjectLockPreflightService(),
            timeProvider: timeProvider);

        var attempt = await client.TryReconnectAsync(
            unityProject,
            dispatchRequest,
            requiredStart,
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                timeProvider),
            CancellationToken.None);

        Assert.True(attempt.IsOwned);
        Assert.True(attempt.Result!.IsSuccess);
        Assert.Empty(launcher.Invocations);
        var requests = IpcRequestAssert.Methods(
            transportClient.Requests,
            UnityIpcMethod.LifecycleStart,
            UnityIpcMethod.LifecycleStart,
            UnityIpcMethod.Compile);
        IpcRequestAssert.AllSessionToken(
            requests,
            sessionToken.GetEncodedValue());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryReconnectAsync_WhenEnvelopeRespondsFromDifferentHost_DoesNotDispatchOrLaunch ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "reconnect-host-mismatch");
        var unityProject =
            ResolvedUnityProjectContextTestFactory
                .CreateForRepositoryRoot(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var timeProvider = new ManualTimeProvider(nowUtc);
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile,
                timeProvider: timeProvider,
                executionTimeout: TimeSpan.FromMinutes(1));
        var requiredStart = CreateRequiredStart(
            unityProject,
            registration,
            ProcessLivenessProbe.CaptureCurrentProcess());
        var differentStart = CreateRequiredStart(
            unityProject,
            registration,
            new ProcessIdentity(int.MaxValue, 1));
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));
        OneshotBootstrapEnvelopeStore.Create(
            unityProject.RepositoryRoot,
            new IpcOneshotBootstrapEnvelope(
                Guid.NewGuid(),
                ProcessLivenessProbe.CaptureCurrentProcess(),
                unityProject.ProjectFingerprint,
                IpcSessionToken.CreateRandom(),
                nowUtc,
                nowUtc.AddMinutes(1),
                UcliIpcEndpointResolver.ResolveDaemonEndpoint(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint).Contract));
        var transportClient = new RecordingUnityIpcTransportClient(
            request => LifecycleExecutionIpcTestResponseFactory
                .CreateResponse(request, differentStart),
            createLifecycleStartResponses: false);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new RecordingUnityProjectLockPreflightService(),
            timeProvider: timeProvider);

        var attempt = await client.TryReconnectAsync(
            unityProject,
            dispatchRequest,
            requiredStart,
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                timeProvider),
            CancellationToken.None);

        Assert.False(attempt.IsOwned);
        Assert.Empty(launcher.Invocations);
        Assert.Single(transportClient.Requests);
        Assert.Equal(
            UnityIpcMethod.LifecycleStart,
            IpcRequestAssert.ParseMethod(
                transportClient.Requests[0]));
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
