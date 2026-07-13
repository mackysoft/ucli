using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Play.Common;

/// <summary> Resolves project, timeout, and GUI daemon session prerequisites for Play Mode commands. </summary>
internal sealed class PlayCommandExecutionContextResolver : IPlayCommandExecutionContextResolver
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IDaemonSessionStore daemonSessionStore;

    /// <summary> Initializes a new instance of the <see cref="PlayCommandExecutionContextResolver" /> class. </summary>
    /// <param name="projectContextResolver"> The project-context resolver dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    public PlayCommandExecutionContextResolver (
        IProjectContextResolver projectContextResolver,
        IDaemonSessionStore daemonSessionStore)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
    }

    /// <inheritdoc />
    public async ValueTask<PlayCommandExecutionContextResolutionResult> ResolveAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        UcliCommand command,
        string sessionNotAvailableMessage,
        string requiresGuiEditorMessage,
        CancellationToken cancellationToken = default)
    {
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sessionNotAvailableMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiresGuiEditorMessage);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return PlayCommandExecutionContextResolutionResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            timeoutMilliseconds,
            command,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return PlayCommandExecutionContextResolutionResult.Failure(timeoutResult.Error!);
        }

        var sessionResult = await daemonSessionStore.ReadAsync(
                context.UnityProject.RepositoryRoot,
                context.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sessionResult.IsSuccess)
        {
            return PlayCommandExecutionContextResolutionResult.Failure(sessionResult.Error!);
        }

        if (!sessionResult.Exists)
        {
            return PlayCommandExecutionContextResolutionResult.Failure(ExecutionError.InternalError(
                sessionNotAvailableMessage,
                PlayModeErrorCodes.PlayModeSessionNotAvailable));
        }

        var session = sessionResult.Session!;
        if (session.EditorMode != DaemonEditorMode.Gui)
        {
            return PlayCommandExecutionContextResolutionResult.Failure(ExecutionError.InternalError(
                requiresGuiEditorMessage,
                PlayModeErrorCodes.PlayModeRequiresGuiEditor));
        }

        var timeout = timeoutResult.Timeout!.Value;
        return PlayCommandExecutionContextResolutionResult.Success(new PlayCommandExecutionContext(
            ProjectContext: context,
            Project: project,
            Session: session,
            Timeout: timeout,
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds)));
    }
}
