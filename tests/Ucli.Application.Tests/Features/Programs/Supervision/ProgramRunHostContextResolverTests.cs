using MackySoft.Ucli.Application.Features.Programs.Supervision;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Supervision;

public sealed class ProgramRunHostContextResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_GuiRequirementWithExplicitOneshot_RejectsBeforeModeOrHostSelection ()
    {
        var mode = new RecordingModeDecision();
        var binding = new RecordingBindingFactory();
        var result = await CreateResolver(mode, binding).ResolveAsync(
            CreateProject(), UnityExecutionMode.Oneshot, CreateDeadline(), CreateAuthorization(),
            new ProgramGuiRequirement(1, "screenshot.game", ScreenshotErrorCodes.ScreenshotRequiresGuiSession));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenshotErrorCodes.ScreenshotRequiresGuiSession, result.Failure!.Code);
        Assert.Equal("/steps/1", result.Failure.InstancePath);
        Assert.Empty(mode.Modes);
        Assert.Equal(0, binding.BindResolvedTargetCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_GuiRequirement_AutoRequestsDaemonAndNeverOneshotFallback ()
    {
        var mode = new RecordingModeDecision();
        var binding = new RecordingBindingFactory();
        var result = await CreateResolver(mode, binding).ResolveAsync(
            CreateProject(), UnityExecutionMode.Auto, CreateDeadline(), CreateAuthorization(),
            new ProgramGuiRequirement(1, "screenshot.game", ScreenshotErrorCodes.ScreenshotRequiresGuiSession));

        Assert.False(result.IsSuccess);
        Assert.Equal([UnityExecutionMode.Daemon], mode.Modes);
        Assert.Equal(0, binding.BindResolvedTargetCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_GuiRequirement_WhenDaemonDecisionSucceeds_BindsTheDaemonTarget ()
    {
        var mode = new RecordingModeDecision(succeed: true);
        var binding = new RecordingBindingFactory();
        var result = await CreateResolver(mode, binding).ResolveAsync(
            CreateProject(), UnityExecutionMode.Daemon, CreateDeadline(), CreateAuthorization(),
            new ProgramGuiRequirement(1, "screenshot.game", ScreenshotErrorCodes.ScreenshotRequiresGuiSession));

        Assert.False(result.IsSuccess);
        Assert.Equal([UnityExecutionMode.Daemon], mode.Modes);
        Assert.Equal(1, binding.BindResolvedTargetCount);
        Assert.Equal(UnityExecutionTarget.Daemon, binding.LastTarget);
    }

    private static ProgramRunHostContextResolver CreateResolver (RecordingModeDecision mode, RecordingBindingFactory binding) => new(mode, binding);
    private static ProjectContext CreateProject () => new(ProjectContextTestFactory.Create().UnityProject, UcliConfig.CreateDefault(), ConfigSource.Default);
    private static ExecutionDeadline CreateDeadline () => ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System);
    private static IpcProgramEffectiveAuthorizationSnapshot CreateAuthorization () => new(false, false, IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(false, false));

    private sealed class RecordingModeDecision (bool succeed = false) : IUnityExecutionModeDecisionService
    {
        public List<UnityExecutionMode> Modes { get; } = [];
        public ValueTask<UnityExecutionModeDecisionResult> DecideAsync (UnityExecutionMode mode, ResolvedUnityProjectContext unityProject, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Modes.Add(mode);
            if (succeed)
            {
                return ValueTask.FromResult(UnityExecutionModeDecisionResult.Success(
                    new UnityExecutionModeDecision(mode, true, UnityExecutionTarget.Daemon, timeout)));
            }
            return ValueTask.FromResult(UnityExecutionModeDecisionResult.Failure(ExecutionError.InternalError("No GUI daemon is available.")));
        }
    }

    private sealed class RecordingBindingFactory : ILifecycleExecutionHostBindingFactory
    {
        public int BindResolvedTargetCount { get; private set; }
        public UnityExecutionTarget? LastTarget { get; private set; }
        public ValueTask<LifecycleExecutionHostBindingResolution> BindAsync (UnityExecutionMode requestedMode, ResolvedUnityProjectContext project, ExecutionDeadline executionDeadline, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<LifecycleExecutionHostBindingResolution> BindResolvedTargetAsync (ResolvedUnityProjectContext project, UnityExecutionTarget target, ExecutionDeadline executionDeadline, CancellationToken cancellationToken = default)
        {
            BindResolvedTargetCount++;
            LastTarget = target;
            return ValueTask.FromResult(LifecycleExecutionHostBindingResolution.FromFailure(
                new UnityRequestFailure(UnityRequestFailureKind.General, new UcliCode("BINDING_STOPPED"), "Stopped after recording target.")));
        }
        public ValueTask<LifecycleExecutionHostBindingResolution> BindReconnectAsync (ResolvedUnityProjectContext project, LifecycleExecutionStartBinding requiredStart, ExecutionDeadline callerWaitDeadline, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
