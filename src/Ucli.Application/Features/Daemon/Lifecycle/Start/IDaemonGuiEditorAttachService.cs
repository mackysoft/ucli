namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Resolves daemon start from an already-open Unity GUI Editor when possible. </summary>
internal interface IDaemonGuiEditorAttachService
{
    /// <summary>
    /// Attempts to attach to an existing GUI Editor for the target project.
    /// Returns <see langword="null" /> when no verified GUI Editor candidate exists and launch flow should continue.
    /// </summary>
    ValueTask<DaemonStartResult?> TryAttachExistingGuiEditorAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        CancellationToken cancellationToken = default);
}
