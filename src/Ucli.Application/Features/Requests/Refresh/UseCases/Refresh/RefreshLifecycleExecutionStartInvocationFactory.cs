using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

/// <summary> Prepares Refresh's fixed Lifecycle Execution invocation from Refresh policy. </summary>
internal interface IRefreshLifecycleExecutionStartInvocationFactory
{
    ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
        AbsolutePath? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);
}

/// <summary> Applies Refresh project and timeout policy before fixing the requested host. </summary>
internal sealed class RefreshLifecycleExecutionStartInvocationFactory : IRefreshLifecycleExecutionStartInvocationFactory
{
    private readonly IProjectContextResolver projectContextResolver;
    private readonly ILifecycleExecutionStartInvocationFactory invocationFactory;
    private readonly TimeProvider timeProvider;

    public RefreshLifecycleExecutionStartInvocationFactory (
        IProjectContextResolver projectContextResolver,
        ILifecycleExecutionStartInvocationFactory invocationFactory,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
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
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(timeoutMilliseconds, UcliCommandIds.Refresh, context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return LifecycleExecutionStartInvocationPreparation.Failed(
                ApplicationFailure.FromExecutionError(timeoutResult.Error!),
                project);
        }

        return await invocationFactory.CreateAsync(
                context,
                requestedMode,
                requestedMode,
                ExecutionDeadline.Start(timeoutResult.Timeout!.Value, timeProvider),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
