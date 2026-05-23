namespace MackySoft.Ucli.Application.Features.Play.Common;

/// <summary> Resolves project, timeout, and GUI daemon session prerequisites for Play Mode commands. </summary>
internal interface IPlayCommandExecutionContextResolver
{
    /// <summary> Resolves one Play Mode lifecycle command context. </summary>
    /// <param name="projectPath"> The optional Unity project path. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout in milliseconds. </param>
    /// <param name="command"> The command whose timeout defaults should be used. </param>
    /// <param name="sessionNotAvailableMessage"> The message used when no registered session exists. </param>
    /// <param name="requiresGuiEditorMessage"> The message used when the registered session is not GUI editor mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The context-resolution result. </returns>
    ValueTask<PlayCommandExecutionContextResolutionResult> ResolveAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        UcliCommand command,
        string sessionNotAvailableMessage,
        string requiresGuiEditorMessage,
        CancellationToken cancellationToken = default);
}
