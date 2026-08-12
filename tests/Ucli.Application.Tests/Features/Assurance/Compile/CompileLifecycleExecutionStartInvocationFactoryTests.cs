using MackySoft.Ucli.Application.Features.Assurance.Compile.Execution;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileLifecycleExecutionStartInvocationFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateAsync_WhenAutoResolvesOneshot_UsesOneDecisionAndOneResolvedTargetBindingWithTheSameDeadline ()
    {
        var context = ProjectContextTestFactory.CreateSingleRootProject();
        var projectResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(context));
        var modeDecision = new RecordingModeDecisionService(UnityExecutionTarget.Oneshot);
        var genericFactory = new RecordingGenericFactory();
        var factory = new CompileLifecycleExecutionStartInvocationFactory(
            projectResolver,
            modeDecision,
            genericFactory,
            new FakeTimeProvider());

        var result = await factory.CreateAsync(
            projectPath: null,
            UnityExecutionMode.Auto,
            timeoutMilliseconds: 10000);

        Assert.True(result.IsSuccess);
        Assert.Single(projectResolver.Invocations);
        var decision = Assert.Single(modeDecision.Invocations);
        var resolved = Assert.Single(genericFactory.ResolvedInvocations);
        Assert.Empty(genericFactory.NormalInvocations);
        Assert.Equal(UnityExecutionMode.Auto, decision.Mode);
        Assert.Equal(UnityExecutionTarget.Oneshot, resolved.Target);
        Assert.Equal(decision.Timeout, resolved.Deadline.Timeout);
        Assert.Equal(UnityExecutionMode.Auto, result.Invocation!.Context.RequestedMode);
        Assert.Equal(UnityExecutionTarget.Oneshot, result.Invocation.Context.HostBinding.Target);
    }

    private sealed class RecordingModeDecisionService : IUnityExecutionModeDecisionService
    {
        private readonly UnityExecutionTarget target;
        private readonly List<Invocation> invocations = [];

        public RecordingModeDecisionService (UnityExecutionTarget target)
        {
            this.target = target;
        }

        public IReadOnlyList<Invocation> Invocations => invocations;

        public ValueTask<UnityExecutionModeDecisionResult> DecideAsync (
            UnityExecutionMode mode,
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            invocations.Add(new Invocation(mode, timeout));
            return ValueTask.FromResult(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(mode, DaemonRunning: false, target, timeout)));
        }

        internal sealed record Invocation (UnityExecutionMode Mode, TimeSpan Timeout);
    }

    private sealed class RecordingGenericFactory : ILifecycleExecutionStartInvocationFactory
    {
        public List<NormalInvocation> NormalInvocations { get; } = [];
        public List<ResolvedInvocation> ResolvedInvocations { get; } = [];

        public ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
            ProjectContext context,
            UnityExecutionMode requestedMode,
            UnityExecutionMode bindingMode,
            ExecutionDeadline executionDeadline,
            CancellationToken cancellationToken = default)
        {
            NormalInvocations.Add(new NormalInvocation(context, requestedMode, bindingMode, executionDeadline));
            throw new InvalidOperationException("Compile must use the resolved target factory path.");
        }

        public ValueTask<LifecycleExecutionStartInvocationPreparation> CreateResolvedTargetAsync (
            ProjectContext context,
            UnityExecutionMode requestedMode,
            UnityExecutionTarget target,
            ExecutionDeadline executionDeadline,
            CancellationToken cancellationToken = default)
        {
            ResolvedInvocations.Add(new ResolvedInvocation(context, requestedMode, target, executionDeadline));
            return ValueTask.FromResult(LifecycleExecutionStartInvocationPreparation.Success(
                new LifecycleExecutionStartInvocation(
                    new LifecycleExecutionFixedContext(
                        context,
                        requestedMode,
                        new RecordingBinding(context.UnityProject, target)),
                    executionDeadline,
                    executionDeadline,
                    NullLifecycleExecutionStartObserver.Instance)));
        }

        internal sealed record NormalInvocation (
            ProjectContext Context,
            UnityExecutionMode RequestedMode,
            UnityExecutionMode BindingMode,
            ExecutionDeadline Deadline);

        internal sealed record ResolvedInvocation (
            ProjectContext Context,
            UnityExecutionMode RequestedMode,
            UnityExecutionTarget Target,
            ExecutionDeadline Deadline);
    }

    private sealed class RecordingBinding : IUnityExecutionHostBinding
    {
        public RecordingBinding (ResolvedUnityProjectContext project, UnityExecutionTarget target)
        {
            Project = project;
            Target = target;
        }

        public ResolvedUnityProjectContext Project { get; }
        public UnityExecutionTarget Target { get; }

        public ValueTask<UnityRequestExecutionResult> StartAsync (
            UcliCommand command,
            UnityRequestPayload payload,
            LifecycleExecutionStartInvocation invocation,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public ValueTask<UnityRequestExecutionResult> ReconnectAsync (
            UcliCommand command,
            UnityRequestPayload payload,
            LifecycleExecutionReconnectInvocation invocation,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public ValueTask DisposeAsync () => ValueTask.CompletedTask;
    }
}
