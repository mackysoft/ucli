using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using static MackySoft.Ucli.Tests.Ipc.UnityIpcRequestExecutorTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcRequestExecutorModeDecisionTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenAutoModeDecisionReturnsContractError_ReturnsContractFailureWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "contract-error");
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => CreateSuccessResponse(Guid.NewGuid()));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => CreateSuccessResponse(Guid.NewGuid()));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = CreateExecutor(
            new StubModeDecisionService(
                UnityExecutionModeDecisionResult.ContractFailure(
                    new UnityExecutionModeDecisionContractError(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        "Daemon is not running for mode=daemon."))),
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionStore("Contract error should not resolve a daemon session."),
                launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        UnityIpcExecutionPathAssert.NoUnityExecutionWasStarted(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenModeDecisionThrows_ReturnsInternalErrorWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "mode-decision-exception");
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new RecordingUnityUcliPluginLocator();
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout)))
            {
                OnDecide = static _ => throw new InvalidOperationException("mode decision failed"),
            },
            new RecordingDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionStore("Mode decision failure should not resolve a daemon session."),
                launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("Failed to decide Unity execution mode.", result.Message, StringComparison.Ordinal);
        UnityIpcExecutionPathAssert.NoPluginVerificationOrUnityExecutionWasStarted(
            pluginLocator,
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenModeDecisionConsumesSharedBudget_ReturnsTimeoutBeforeDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "mode-decision-budget");
        var timeProvider = new ManualTimeProvider();
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var modeDecisionService = new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
            new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                true,
                UnityExecutionTarget.Daemon,
                TimeSpan.FromMilliseconds(100))))
        {
            TimeProvider = timeProvider,
            OnDecide = static context =>
            {
                ((ManualTimeProvider)context.TimeProvider).Advance(TimeSpan.FromMilliseconds(120));
            },
        };
        var executor = CreateExecutor(
            modeDecisionService,
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionStore("Mode decision timeout should not resolve a daemon session."),
                launcher),
            timeProvider);

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(100),
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        UnityExecutionModeDecisionServiceAssert.DecisionAttemptedWithTimeout(
            modeDecisionService,
            TimeSpan.FromMilliseconds(100));
        UnityIpcExecutionPathAssert.NoUnityExecutionWasStarted(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenReconnectRequiresPersistedStart_BypassesModeDecisionAndUsesExistingProvider ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-ipc-request-executor",
            "reconnect-bypasses-mode-decision");
        var unityProject =
            ResolvedUnityProjectContextTestFactory
                .CreateForRepositoryRoot(scope.FullPath);
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile);
        var definitionDigest =
            LifecycleExecutionDefinitionDigest.Calculate(
                registration.Definition);
        var requiredStart = new LifecycleExecutionStartBinding(
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
                ProcessLivenessProbe.CaptureCurrentProcess(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid()),
            new UnityEditorGenerationSnapshot(1, 2, 3, 4),
            registration.DeadlineUtc,
            registration.StartedAtUtc);
        var expectedFailure = UnityRequestExecutionResult.Failure(
            new UnityRequestFailure(
                UnityRequestFailureKind.General,
                UcliCoreErrorCodes.InternalError,
                "existing provider sentinel"));
        var daemonClient = new RecordingUnityIpcClient(
            UnityExecutionTarget.Daemon,
            expectedFailure);
        var oneshotClient = new RecordingUnityIpcClient(
            UnityExecutionTarget.Oneshot);
        var modeDecisionService = new StubModeDecisionService(
            UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout)))
        {
            OnDecide = static _ => throw new Xunit.Sdk.XunitException(
                "Reconnect must not re-evaluate Unity execution mode."),
        };
        var executor = CreateExecutor(
            modeDecisionService,
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            [daemonClient, oneshotClient]);

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Compile,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            unityProject,
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));

        Assert.Same(expectedFailure, result);
        Assert.Empty(modeDecisionService.Invocations);
        Assert.Single(daemonClient.Invocations);
        Assert.Empty(oneshotClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenReconnectFixedProcessGenerationHasExited_ReportsExactHostExitWithoutCallingProviders ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-ipc-request-executor",
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
        var daemonClient = new RecordingUnityIpcClient(
            UnityExecutionTarget.Daemon);
        var oneshotClient = new RecordingUnityIpcClient(
            UnityExecutionTarget.Oneshot);
        var modeDecisionService = new StubModeDecisionService(
            UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout)))
        {
            OnDecide = static _ => throw new Xunit.Sdk.XunitException(
                "Reconnect must not re-evaluate Unity execution mode."),
        };
        var executor = CreateExecutor(
            modeDecisionService,
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            [daemonClient, oneshotClient]);

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Compile,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            unityProject,
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            EditorLifecycleErrorCodes.EditorUnavailable,
            result.ErrorCode);
        Assert.Equal(
            exitedGeneration,
            result.ConfirmedHostExit!.Process);
        Assert.Empty(modeDecisionService.Invocations);
        Assert.Empty(daemonClient.Invocations);
        Assert.Empty(oneshotClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenLiveReconnectHostHasNoOwningProvider_ReturnsUnavailableWithoutHostExitObservation ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-ipc-request-executor",
            "reconnect-provider-mismatch");
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
        var daemonClient = new RecordingUnityIpcClient(
            UnityExecutionTarget.Daemon)
        {
            OwnsReconnect = false,
        };
        var oneshotClient = new RecordingUnityIpcClient(
            UnityExecutionTarget.Oneshot)
        {
            OwnsReconnect = false,
        };
        var modeDecisionService = new StubModeDecisionService(
            UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout)))
        {
            OnDecide = static _ => throw new Xunit.Sdk.XunitException(
                "Reconnect must not re-evaluate Unity execution mode."),
        };
        var executor = CreateExecutor(
            modeDecisionService,
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            [daemonClient, oneshotClient]);

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Compile,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            unityProject,
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            EditorLifecycleErrorCodes.EditorUnavailable,
            result.ErrorCode);
        Assert.Same(requiredStart, result.LifecycleExecutionStart);
        Assert.Null(result.ConfirmedHostExit);
        Assert.Empty(modeDecisionService.Invocations);
        Assert.Single(daemonClient.Invocations);
        Assert.Single(oneshotClient.Invocations);
    }

    private static LifecycleExecutionStartBinding CreateRequiredStart (
        ResolvedUnityProjectContext unityProject,
        LifecycleExecutionRegistration registration,
        ProcessIdentity process)
    {
        return new LifecycleExecutionStartBinding(
            new ActiveExecutionRef(
                registration.Definition.ExecutionKind,
                registration.ExecutionId,
                LifecycleExecutionDefinitionDigest.Calculate(
                    registration.Definition),
                new ExecutionState(TextVocabulary.GetText(
                    LifecycleExecutionState.Registered)),
                new ExecutionStatusLocator(
                    $"lifecycle-executions/{registration.ExecutionId:N}/status.json")),
            new UnityProjectIdentity(
                unityProject.UnityProjectRoot.Value,
                unityProject.ProjectFingerprint,
                unityProject.UnityVersion),
            new LifecycleExecutionHostRegistration(
                process,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid()),
            new UnityEditorGenerationSnapshot(1, 2, 3, 4),
            registration.DeadlineUtc,
            registration.StartedAtUtc);
    }
}
