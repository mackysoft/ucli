using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Tests.Shared.Execution.UnityRequest;

public sealed class LifecycleExecutionStartInvocationFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateAsync_BindsOnce_PreservesRequestedModeAndUsesOneDeadline ()
    {
        var context = ProjectContextTestFactory.CreateSingleRootProject();
        var binding = new RecordingBinding(context.UnityProject, UnityExecutionTarget.Oneshot);
        var bindings = new RecordingBindingFactory(binding);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(10), new FakeTimeProvider());
        var factory = new LifecycleExecutionStartInvocationFactory(bindings);

        var result = await factory.CreateAsync(
            context,
            UnityExecutionMode.Auto,
            UnityExecutionMode.Oneshot,
            deadline);

        Assert.True(result.IsSuccess);
        var invocation = result.Invocation!;
        var bind = Assert.Single(bindings.Invocations);
        Assert.Equal(UnityExecutionMode.Oneshot, bind.Mode);
        Assert.Same(deadline, bind.Deadline);
        Assert.Equal(UnityExecutionMode.Auto, invocation.Context.RequestedMode);
        Assert.Same(deadline, invocation.ExecutionDeadline);
        Assert.Same(deadline, invocation.CallerWaitDeadline);
        Assert.Equal(0, binding.StartCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateAsync_WhenBindingIsForeign_DisposesItAndDoesNotCreateAnInvocation ()
    {
        var context = ProjectContextTestFactory.CreateSingleRootProject();
        var foreign = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var binding = new RecordingBinding(foreign.UnityProject, UnityExecutionTarget.Oneshot);
        var factory = new LifecycleExecutionStartInvocationFactory(new RecordingBindingFactory(binding));

        var result = await factory.CreateResolvedTargetAsync(
            context,
            UnityExecutionMode.Oneshot,
            UnityExecutionTarget.Oneshot,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(10), new FakeTimeProvider()));

        Assert.False(result.IsSuccess);
        Assert.Equal(LifecycleExecutionErrorCodes.HostMismatch, result.Failure!.Code);
        Assert.Equal(1, binding.DisposeCount);
        Assert.Equal(0, binding.StartCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateResolvedTargetAsync_PreservesTargetAndDeadlineWithoutDispatch ()
    {
        var context = ProjectContextTestFactory.CreateSingleRootProject();
        var binding = new RecordingBinding(context.UnityProject, UnityExecutionTarget.Oneshot);
        var bindings = new RecordingBindingFactory(binding);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(10), new FakeTimeProvider());
        var factory = new LifecycleExecutionStartInvocationFactory(bindings);

        var result = await factory.CreateResolvedTargetAsync(
            context,
            UnityExecutionMode.Auto,
            UnityExecutionTarget.Oneshot,
            deadline);

        Assert.True(result.IsSuccess);
        var bind = Assert.Single(bindings.Invocations);
        Assert.Equal(UnityExecutionMode.Oneshot, bind.Mode);
        Assert.Same(deadline, bind.Deadline);
        Assert.Same(deadline, result.Invocation!.ExecutionDeadline);
        Assert.Same(deadline, result.Invocation.CallerWaitDeadline);
        Assert.Equal(0, binding.StartCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateResolvedTargetAsync_WhenBindingFails_MapsTheFailureWithoutDispatch ()
    {
        var context = ProjectContextTestFactory.CreateSingleRootProject();
        var factory = new LifecycleExecutionStartInvocationFactory(new RecordingBindingFactory(
            new UnityRequestFailure(UnityRequestFailureKind.General, ExecutionErrorCodes.IpcTimeout, "Binding failed.")));

        var result = await factory.CreateResolvedTargetAsync(
            context,
            UnityExecutionMode.Auto,
            UnityExecutionTarget.Daemon,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(10), new FakeTimeProvider()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Failure!.Code);
    }

    private sealed class RecordingBindingFactory : ILifecycleExecutionHostBindingFactory
    {
        private readonly IUnityExecutionHostBinding? binding;
        private readonly UnityRequestFailure? failure;

        public RecordingBindingFactory (IUnityExecutionHostBinding binding)
        {
            this.binding = binding;
        }

        public RecordingBindingFactory (UnityRequestFailure failure)
        {
            this.failure = failure;
        }

        public List<Invocation> Invocations { get; } = [];

        public ValueTask<LifecycleExecutionHostBindingResolution> BindAsync (
            UnityExecutionMode mode,
            ResolvedUnityProjectContext project,
            ExecutionDeadline executionDeadline,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(new Invocation(mode, project, executionDeadline));
            return ValueTask.FromResult(failure is null
                ? LifecycleExecutionHostBindingResolution.Success(binding!)
                : LifecycleExecutionHostBindingResolution.FromFailure(failure));
        }

        public ValueTask<LifecycleExecutionHostBindingResolution> BindReconnectAsync (
            ResolvedUnityProjectContext project,
            LifecycleExecutionStartBinding requiredStart,
            ExecutionDeadline callerWaitDeadline,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public ValueTask<LifecycleExecutionHostBindingResolution> BindResolvedTargetAsync (
            ResolvedUnityProjectContext project,
            UnityExecutionTarget target,
            ExecutionDeadline executionDeadline,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(new Invocation(
                target == UnityExecutionTarget.Oneshot ? UnityExecutionMode.Oneshot : UnityExecutionMode.Daemon,
                project,
                executionDeadline));
            return ValueTask.FromResult(failure is null
                ? LifecycleExecutionHostBindingResolution.Success(binding!)
                : LifecycleExecutionHostBindingResolution.FromFailure(failure));
        }

        internal sealed record Invocation (
            UnityExecutionMode Mode,
            ResolvedUnityProjectContext Project,
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
        public int StartCount { get; private set; }
        public int DisposeCount { get; private set; }

        public ValueTask<UnityRequestExecutionResult> StartAsync (
            UcliCommand command,
            UnityRequestPayload payload,
            LifecycleExecutionStartInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            StartCount++;
            throw new InvalidOperationException();
        }

        public ValueTask<UnityRequestExecutionResult> ReconnectAsync (
            UcliCommand command,
            UnityRequestPayload payload,
            LifecycleExecutionReconnectInvocation invocation,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public ValueTask DisposeAsync ()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
