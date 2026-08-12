using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using ExecutionMode = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Creates one fixed Lifecycle Execution start invocation from already resolved action facts. </summary>
internal interface ILifecycleExecutionStartInvocationFactory
{
    ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
        ProjectContext context,
        ExecutionMode requestedMode,
        ExecutionMode bindingMode,
        ExecutionDeadline executionDeadline,
        CancellationToken cancellationToken = default);

    ValueTask<LifecycleExecutionStartInvocationPreparation> CreateResolvedTargetAsync (
        ProjectContext context,
        ExecutionMode requestedMode,
        UnityExecutionTarget target,
        ExecutionDeadline executionDeadline,
        CancellationToken cancellationToken = default);
}

/// <summary> Contains either a fixed start invocation or the typed preparation failure and project facts. </summary>
internal sealed record LifecycleExecutionStartInvocationPreparation
{
    private LifecycleExecutionStartInvocationPreparation (
        LifecycleExecutionStartInvocation? invocation,
        ProjectIdentityInfo? project,
        ApplicationFailure? failure)
    {
        if ((invocation is not null) == (failure is not null))
        {
            throw new ArgumentException("A Lifecycle Execution preparation must either succeed or fail.");
        }

        Invocation = invocation;
        Project = project;
        Failure = failure;
    }

    public LifecycleExecutionStartInvocation? Invocation { get; }

    public ProjectIdentityInfo? Project { get; }

    public ApplicationFailure? Failure { get; }

    public bool IsSuccess => Invocation is not null;

    public static LifecycleExecutionStartInvocationPreparation Success (LifecycleExecutionStartInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return new LifecycleExecutionStartInvocationPreparation(
            invocation,
            ProjectIdentityInfo.From(invocation.Context.Project.UnityProject),
            failure: null);
    }

    public static LifecycleExecutionStartInvocationPreparation Failed (
        ApplicationFailure failure,
        ProjectIdentityInfo? project)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new LifecycleExecutionStartInvocationPreparation(null, project, failure);
    }
}

/// <summary> Fixes exactly one verified Unity host binding without resolving action policy or dispatching an action. </summary>
internal sealed class LifecycleExecutionStartInvocationFactory : ILifecycleExecutionStartInvocationFactory
{
    private readonly ILifecycleExecutionHostBindingFactory hostBindingFactory;

    public LifecycleExecutionStartInvocationFactory (ILifecycleExecutionHostBindingFactory hostBindingFactory)
    {
        this.hostBindingFactory = hostBindingFactory ?? throw new ArgumentNullException(nameof(hostBindingFactory));
    }

    public async ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
        ProjectContext context,
        ExecutionMode requestedMode,
        ExecutionMode bindingMode,
        ExecutionDeadline executionDeadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executionDeadline);
        cancellationToken.ThrowIfCancellationRequested();
        var bindingResult = await hostBindingFactory.BindAsync(
                bindingMode,
                context.UnityProject,
                executionDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        return await CreateFromBindingAsync(context, requestedMode, executionDeadline, bindingResult)
            .ConfigureAwait(false);
    }

    public async ValueTask<LifecycleExecutionStartInvocationPreparation> CreateResolvedTargetAsync (
        ProjectContext context,
        ExecutionMode requestedMode,
        UnityExecutionTarget target,
        ExecutionDeadline executionDeadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executionDeadline);
        cancellationToken.ThrowIfCancellationRequested();
        var bindingResult = await hostBindingFactory.BindResolvedTargetAsync(
                context.UnityProject,
                target,
                executionDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        return await CreateFromBindingAsync(context, requestedMode, executionDeadline, bindingResult)
            .ConfigureAwait(false);
    }

    private static async ValueTask<LifecycleExecutionStartInvocationPreparation> CreateFromBindingAsync (
        ProjectContext context,
        ExecutionMode requestedMode,
        ExecutionDeadline executionDeadline,
        LifecycleExecutionHostBindingResolution bindingResult)
    {
        var project = ProjectIdentityInfo.From(context.UnityProject);
        if (!bindingResult.IsSuccess)
        {
            var failure = bindingResult.Failure is null
                ? ApplicationFailure.Timeout(
                    "The Lifecycle Execution deadline elapsed before the Unity host binding was fixed.",
                    LifecycleExecutionErrorCodes.DeadlineExceeded)
                : ApplicationFailure.FromCode(bindingResult.Failure.Code, bindingResult.Failure.Message);
            return LifecycleExecutionStartInvocationPreparation.Failed(failure, project);
        }

        var binding = bindingResult.Binding!;
        if (binding.Project != context.UnityProject)
        {
            await binding.DisposeAsync().ConfigureAwait(false);
            return LifecycleExecutionStartInvocationPreparation.Failed(
                ApplicationFailure.EnvironmentError(
                    "The fixed Unity host binding belongs to another project.",
                    LifecycleExecutionErrorCodes.HostMismatch),
                project);
        }

        return LifecycleExecutionStartInvocationPreparation.Success(
            new LifecycleExecutionStartInvocation(
                new LifecycleExecutionFixedContext(context, requestedMode, binding),
                executionDeadline,
                executionDeadline,
                NullLifecycleExecutionStartObserver.Instance));
    }
}
