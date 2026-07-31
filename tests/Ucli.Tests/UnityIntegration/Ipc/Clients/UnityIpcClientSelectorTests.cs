using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcClientSelectorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Select_WithRegisteredTarget_ReturnsMatchingClient ()
    {
        var daemonClient = new RecordingUnityIpcClient(UnityExecutionTarget.Daemon);
        var oneshotClient = new RecordingUnityIpcClient(UnityExecutionTarget.Oneshot);
        var selector = new UnityIpcClientSelector([daemonClient, oneshotClient]);

        var selected = selector.Select(UnityExecutionTarget.Oneshot);

        Assert.Same(oneshotClient, selected);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Select_WithMissingTarget_ThrowsInvalidOperationException ()
    {
        var selector = new UnityIpcClientSelector([new RecordingUnityIpcClient(UnityExecutionTarget.Daemon)]);

        var exception = Assert.Throws<InvalidOperationException>(() => selector.Select(UnityExecutionTarget.Oneshot));

        Assert.Contains("Oneshot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithDuplicateTarget_ThrowsInvalidOperationException ()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new UnityIpcClientSelector(
        [
            new RecordingUnityIpcClient(UnityExecutionTarget.Daemon),
            new RecordingUnityIpcClient(UnityExecutionTarget.Daemon),
        ]));

        Assert.Contains("Daemon", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((int)ProcessIdentityObservation.Same)]
    [InlineData((int)ProcessIdentityObservation.Unobservable)]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenNoProviderProvesHostAndExitIsNotConfirmed_ReturnsUnavailableWithoutClaimingExit (
        int processObservationValue)
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-ipc-client-selector",
            "reconnect-provider-unavailable");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile);
        var requiredStart = CreateRequiredStart(
            unityProject,
            registration);
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));
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
        var selector = new UnityIpcClientSelector(
            [daemonClient, oneshotClient],
            _ => (ProcessIdentityObservation)processObservationValue);

        var result = await selector.ReconnectAsync(
            unityProject,
            dispatchRequest,
            requiredStart,
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            EditorLifecycleErrorCodes.EditorUnavailable,
            result.ErrorCode);
        Assert.NotEqual(
            LifecycleExecutionErrorCodes.HostMismatch,
            result.ErrorCode);
        Assert.Same(
            requiredStart,
            result.LifecycleExecutionStart);
        Assert.Null(result.ConfirmedHostExit);
        Assert.Single(daemonClient.Invocations);
        Assert.Single(oneshotClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenProcessExitIsConfirmed_ReturnsConfirmedHostExitWithoutProviderAttempt ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-ipc-client-selector",
            "reconnect-confirmed-host-exit");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile);
        var requiredStart = CreateRequiredStart(
            unityProject,
            registration);
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Compile(
                registration,
                requiredStart));
        var daemonClient = new RecordingUnityIpcClient(
            UnityExecutionTarget.Daemon);
        var oneshotClient = new RecordingUnityIpcClient(
            UnityExecutionTarget.Oneshot);
        var selector = new UnityIpcClientSelector(
            [daemonClient, oneshotClient],
            _ => ProcessIdentityObservation.ConfirmedExitedOrReplaced);

        var result = await selector.ReconnectAsync(
            unityProject,
            dispatchRequest,
            requiredStart,
            ExecutionDeadline.Start(
                TimeSpan.FromSeconds(30),
                TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(requiredStart, result.LifecycleExecutionStart);
        Assert.Equal(
            requiredStart.Host.Process,
            result.ConfirmedHostExit?.Process);
        Assert.Empty(daemonClient.Invocations);
        Assert.Empty(oneshotClient.Invocations);
    }

    private static LifecycleExecutionStartBinding CreateRequiredStart (
        ResolvedUnityProjectContext unityProject,
        LifecycleExecutionRegistration registration)
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
                ProcessLivenessProbe.CaptureCurrentProcess(),
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new UnityEditorGenerationSnapshot(10, 20, 30, 40),
            registration.DeadlineUtc,
            registration.StartedAtUtc);
    }
}
