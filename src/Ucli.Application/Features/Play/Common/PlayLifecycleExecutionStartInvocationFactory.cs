namespace MackySoft.Ucli.Application.Features.Play.Common;

/// <summary> Prepares Play transition invocations through the GUI-daemon session policy. </summary>
internal interface IPlayLifecycleExecutionStartInvocationFactory
{
    ValueTask<LifecycleExecutionStartInvocationPreparation> CreateEnterAsync (
        AbsolutePath? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);

    ValueTask<LifecycleExecutionStartInvocationPreparation> CreateExitAsync (
        AbsolutePath? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);
}

/// <summary> Applies Play transition session policy before fixing the daemon host. </summary>
internal sealed class PlayLifecycleExecutionStartInvocationFactory : IPlayLifecycleExecutionStartInvocationFactory
{
    private readonly IPlayCommandExecutionContextResolver playContextResolver;
    private readonly ILifecycleExecutionStartInvocationFactory invocationFactory;
    private readonly TimeProvider timeProvider;

    public PlayLifecycleExecutionStartInvocationFactory (
        IPlayCommandExecutionContextResolver playContextResolver,
        ILifecycleExecutionStartInvocationFactory invocationFactory,
        TimeProvider timeProvider)
    {
        this.playContextResolver = playContextResolver ?? throw new ArgumentNullException(nameof(playContextResolver));
        this.invocationFactory = invocationFactory ?? throw new ArgumentNullException(nameof(invocationFactory));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public ValueTask<LifecycleExecutionStartInvocationPreparation> CreateEnterAsync (
        AbsolutePath? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        return CreateAsync(
            projectPath,
            timeoutMilliseconds,
            UcliCommandIds.PlayEnter,
            "Registered GUI daemon session is not available for Play Mode enter.",
            "Play Mode enter requires a registered GUI daemon session.",
            cancellationToken);
    }

    public ValueTask<LifecycleExecutionStartInvocationPreparation> CreateExitAsync (
        AbsolutePath? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        return CreateAsync(
            projectPath,
            timeoutMilliseconds,
            UcliCommandIds.PlayExit,
            "Registered GUI daemon session is not available for Play Mode exit.",
            "Play Mode exit requires a registered GUI daemon session.",
            cancellationToken);
    }

    private async ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
        AbsolutePath? projectPath,
        int? timeoutMilliseconds,
        UcliCommand command,
        string sessionNotAvailableMessage,
        string requiresGuiEditorMessage,
        CancellationToken cancellationToken)
    {
        var contextResult = await playContextResolver.ResolveAsync(
                projectPath,
                timeoutMilliseconds,
                command,
                sessionNotAvailableMessage,
                requiresGuiEditorMessage,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return LifecycleExecutionStartInvocationPreparation.Failed(
                ApplicationFailure.FromExecutionError(contextResult.Error!),
                project: null);
        }

        var context = contextResult.Context!;
        return await invocationFactory.CreateAsync(
                context.ProjectContext,
                UnityExecutionMode.Daemon,
                UnityExecutionMode.Daemon,
                ExecutionDeadline.Start(context.Timeout, timeProvider),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
