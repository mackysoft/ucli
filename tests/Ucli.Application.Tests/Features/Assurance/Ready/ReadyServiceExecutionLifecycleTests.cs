using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Ready;

using static MackySoft.Ucli.Application.Tests.Features.Assurance.Ready.ReadyServiceTestSupport;

public sealed class ReadyServiceExecutionLifecycleTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExecutionTarget_ReturnsTypedReadyVerifierIdentity ()
    {
        var service = CreateService();

        var result = await service.ExecuteAsync(CreateExecutionInput());

        var completed = Assert.IsType<ReadyExecutionResult.CompletedResult>(result);
        var output = completed.Output;
        var verifier = Assert.Single(output.Verifiers);
        var verifierId = Assert.IsType<AssuranceVerifierId>(verifier.Id);
        Assert.Equal("ready.lifecycle", verifierId.Value);
        Assert.Equal(AssuranceVerifierKind.Ready, Assert.IsType<AssuranceVerifierKind>(verifier.Kind));
        Assert.Equal(verifierId, Assert.IsType<AssuranceVerifierId>(Assert.Single(output.Claims).VerifierRef));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithAutoResolvedToOneshot_ReturnsProbeOnlyValidityWithoutReusableSession ()
    {
        var unityRequestExecutor = new RecordingUnityRequestExecutor(CreateReadyPingSuccess());
        var service = CreateService(
            modeDecisionService: CreateModeDecisionService(
                UnityExecutionMode.Auto,
                daemonRunning: false,
                UnityExecutionTarget.Oneshot),
            unityRequestExecutor: unityRequestExecutor);

        var result = await service.ExecuteAsync(CreateExecutionInput());

        var completed = Assert.IsType<ReadyExecutionResult.CompletedResult>(result);
        var output = completed.Output;
        Assert.Equal(Verdict.Pass, output.Verdict);
        Assert.Equal(AssuranceResolvedExecutionMode.Oneshot, output.ResolvedMode);
        Assert.Equal(AssuranceSessionKind.TransientProbe, output.SessionKind);
        Assert.NotNull(output.Lifecycle);
        Assert.NotNull(output.Lifecycle.PlayMode);
        Assert.Equal(UnityEditorPlayModeState.Stopped, output.Lifecycle.PlayMode.State);
        Assert.Equal(UnityEditorPlayModeTransition.None, output.Lifecycle.PlayMode.Transition);
        var claim = Assert.Single(output.Claims);
        Assert.IsType<ProbeOnlyReadyClaimValidityOutput>(claim.Validity);
        UnityRequestExecutorInvocationAssert.ReadyPingOnce(
            unityRequestExecutor,
            expectedFailFast: false);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithBlockedDaemonLifecycle_ReturnsFailedClaimPacket ()
    {
        var service = CreateService(
            modeDecisionService: CreateModeDecisionService(
                UnityExecutionMode.Daemon,
                daemonRunning: true,
                UnityExecutionTarget.Daemon),
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(CreateReadyPingResponse(
                lifecycleState: UnityEditorLifecycleState.CompileFailed)));

        var result = await service.ExecuteAsync(CreateExecutionInput(UnityExecutionMode.Daemon, failFast: true));

        var completed = Assert.IsType<ReadyExecutionResult.CompletedResult>(result);
        var output = completed.Output;
        Assert.Equal(Verdict.Fail, output.Verdict);
        Assert.Equal(AssuranceResolvedExecutionMode.Daemon, output.ResolvedMode);
        var claim = Assert.Single(output.Claims);
        Assert.Equal(AssuranceClaimStatus.Failed, claim.Status);
        var validity = Assert.IsType<SessionBoundReadyClaimValidityOutput>(claim.Validity);
        Assert.False(validity.GuaranteesReusableSession);
        Assert.Contains(claim.Evidence, static evidence => evidence.Kind == ReadyEvidenceKind.ReadinessDecision);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOneshotObservedLifecycleFailure_ReturnsFailedClaimPacket ()
    {
        var service = CreateService(
            modeDecisionService: CreateModeDecisionService(
                UnityExecutionMode.Auto,
                daemonRunning: false,
                UnityExecutionTarget.Oneshot),
            unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                UnityRequestFailureKind.General,
                EditorLifecycleErrorCodes.EditorCompileFailed,
                "Unity editor has script compilation failures.",
                startupFailure: null))));

        var result = await service.ExecuteAsync(CreateExecutionInput());

        var completed = Assert.IsType<ReadyExecutionResult.CompletedResult>(result);
        var output = completed.Output;
        Assert.Equal(Verdict.Fail, output.Verdict);
        var claim = Assert.Single(output.Claims);
        Assert.Equal(AssuranceClaimStatus.Failed, claim.Status);
        Assert.Contains(claim.Evidence, static evidence => evidence.Kind == ReadyEvidenceKind.ReadinessDecision);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithDomainReloadingLifecycle_ReturnsFailedClaimWithoutWaiting ()
    {
        var service = CreateService(
            modeDecisionService: CreateModeDecisionService(
                UnityExecutionMode.Auto,
                daemonRunning: false,
                UnityExecutionTarget.Oneshot),
            unityRequestExecutor: new RecordingUnityRequestExecutor(CreateReadyPingSuccess(
                lifecycleState: UnityEditorLifecycleState.DomainReloading)));

        var result = await service.ExecuteAsync(CreateExecutionInput());

        var completed = Assert.IsType<ReadyExecutionResult.CompletedResult>(result);
        var output = completed.Output;
        Assert.Equal(Verdict.Fail, output.Verdict);
        var claim = Assert.Single(output.Claims);
        Assert.Equal(AssuranceClaimStatus.Failed, claim.Status);
        Assert.Contains(claim.Evidence, static evidence => evidence.Kind == ReadyEvidenceKind.ReadinessDecision);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOneshotFailFast_PropagatesFailFastToPingPayload ()
    {
        var unityRequestExecutor = new RecordingUnityRequestExecutor(CreateReadyPingSuccess());
        var service = CreateService(
            modeDecisionService: CreateModeDecisionService(
                UnityExecutionMode.Oneshot,
                daemonRunning: false,
                UnityExecutionTarget.Oneshot),
            unityRequestExecutor: unityRequestExecutor);

        var result = await service.ExecuteAsync(CreateExecutionInput(UnityExecutionMode.Oneshot, failFast: true));

        Assert.IsType<ReadyExecutionResult.CompletedResult>(result);
        UnityRequestExecutorInvocationAssert.ReadyPingOnce(
            unityRequestExecutor,
            expectedFailFast: true);
    }
}
