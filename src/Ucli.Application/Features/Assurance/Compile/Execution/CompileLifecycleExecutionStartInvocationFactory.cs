using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Execution;

/// <summary> Prepares Compile's fixed Lifecycle Execution invocation from Compile policy. </summary>
internal interface ICompileLifecycleExecutionStartInvocationFactory
{
    ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
        AbsolutePath? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);
}

/// <summary> Applies Compile project, timeout, and execution-mode policy before host binding. </summary>
internal sealed class CompileLifecycleExecutionStartInvocationFactory : ICompileLifecycleExecutionStartInvocationFactory
{
    private readonly IProjectContextResolver projectContextResolver;
    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;
    private readonly ILifecycleExecutionStartInvocationFactory invocationFactory;
    private readonly TimeProvider timeProvider;

    public CompileLifecycleExecutionStartInvocationFactory (
        IProjectContextResolver projectContextResolver,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        ILifecycleExecutionStartInvocationFactory invocationFactory,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.invocationFactory = invocationFactory ?? throw new ArgumentNullException(nameof(invocationFactory));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
        AbsolutePath? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        var contextResult = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return LifecycleExecutionStartInvocationPreparation.Failed(
                ApplicationFailure.FromExecutionError(contextResult.Error!),
                project: null);
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(timeoutMilliseconds, UcliCommandIds.Compile, context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return LifecycleExecutionStartInvocationPreparation.Failed(
                ApplicationFailure.FromExecutionError(timeoutResult.Error!),
                project);
        }

        var deadline = ExecutionDeadline.Start(timeoutResult.Timeout!.Value, timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
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

        return await invocationFactory.CreateResolvedTargetAsync(
                context,
                requestedMode,
                modeDecisionResult.Decision!.Target,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
