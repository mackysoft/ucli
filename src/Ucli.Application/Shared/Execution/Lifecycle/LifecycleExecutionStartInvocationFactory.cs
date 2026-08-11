using MackySoft.Ucli.Application.Shared.Context;
using ExecutionMode = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Prepares one fixed Lifecycle Execution start invocation from typed caller input. </summary>
internal interface ILifecycleExecutionStartInvocationFactory
{
    ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
        string? projectPath,
        ExecutionMode requestedMode,
        int? timeoutMilliseconds,
        UcliCommand command,
        bool decideMode,
        CancellationToken cancellationToken = default);
}

/// <summary> Contains either a complete fixed start invocation or the pre-start failure and project facts. </summary>
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

    public static LifecycleExecutionStartInvocationPreparation Success (
        LifecycleExecutionStartInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return new LifecycleExecutionStartInvocationPreparation(
            invocation,
            ProjectIdentityInfo.From(invocation.Context.Project.UnityProject),
            failure: null);
    }

    public static LifecycleExecutionStartInvocationPreparation Failed (
        ApplicationFailure failure,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new LifecycleExecutionStartInvocationPreparation(null, project, failure);
    }
}

/// <summary> Creates fixed invocations through the application project, timeout, mode, and host-binding policies. </summary>
internal sealed class LifecycleExecutionStartInvocationFactory : ILifecycleExecutionStartInvocationFactory
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly TimeProvider timeProvider;

    public LifecycleExecutionStartInvocationFactory (
        IProjectContextResolver projectContextResolver,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        IUnityRequestExecutor unityRequestExecutor,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
        string? projectPath,
        ExecutionMode requestedMode,
        int? timeoutMilliseconds,
        UcliCommand command,
        bool decideMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        var contextResult = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return LifecycleExecutionStartInvocationPreparation.Failed(
                ApplicationFailure.FromExecutionError(contextResult.Error!));
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(timeoutMilliseconds, command, context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return LifecycleExecutionStartInvocationPreparation.Failed(
                ApplicationFailure.FromExecutionError(timeoutResult.Error!),
                project);
        }

        var executionDeadline = ExecutionDeadline.Start(timeoutResult.Timeout!.Value, timeProvider);
        var bindingMode = requestedMode;
        if (decideMode)
        {
            if (!executionDeadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
            {
                return LifecycleExecutionStartInvocationPreparation.Failed(
                    ApplicationFailure.Timeout(
                        "Compile execution deadline elapsed before Unity execution mode was decided.",
                        LifecycleExecutionErrorCodes.DeadlineExceeded),
                    project);
            }

            var modeDecisionResult = await executionModeDecisionService.DecideAsync(
                    requestedMode,
                    context.UnityProject,
                    modeDecisionTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!modeDecisionResult.IsSuccess)
            {
                var failure = modeDecisionResult.HasContractError
                    ? ApplicationFailure.EnvironmentError(
                        modeDecisionResult.ContractError!.Message,
                        modeDecisionResult.ContractError.Code)
                    : ApplicationFailure.FromExecutionError(modeDecisionResult.Error!);
                return LifecycleExecutionStartInvocationPreparation.Failed(failure, project);
            }

            bindingMode = UnityExecutionTargetModeMapper.ToExplicitMode(modeDecisionResult.Decision!.Target);
        }

        var bindingResult = await unityRequestExecutor.BindAsync(
                bindingMode,
                context.UnityProject,
                executionDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!bindingResult.IsSuccess)
        {
            var failure = bindingResult.Failure is null
                ? ApplicationFailure.Timeout(
                    $"{command.Name} execution deadline elapsed before the Unity host binding was fixed.",
                    LifecycleExecutionErrorCodes.DeadlineExceeded)
                : ApplicationFailure.FromCode(bindingResult.Failure.Code, bindingResult.Failure.Message);
            return LifecycleExecutionStartInvocationPreparation.Failed(failure, project);
        }

        return LifecycleExecutionStartInvocationPreparation.Success(
            new LifecycleExecutionStartInvocation(
                new LifecycleExecutionFixedContext(context, requestedMode, bindingResult.Binding!),
                executionDeadline,
                executionDeadline.CreateCompletionDeadline(LifecycleExecutionTiming.ResponseDeliveryGrace),
                NullLifecycleExecutionStartObserver.Instance));
    }
}
