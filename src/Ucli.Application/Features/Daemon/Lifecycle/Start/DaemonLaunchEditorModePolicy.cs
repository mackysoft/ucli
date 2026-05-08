using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Resolves daemon Editor mode values that are supported by fresh launch orchestration. </summary>
internal static class DaemonLaunchEditorModePolicy
{
    /// <summary> Resolves the effective fresh-launch Editor mode. </summary>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="launchEditorMode"> The supported launch Editor mode when resolution succeeds. </param>
    /// <param name="error"> The resolution error when resolution fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the launch mode is supported; otherwise <see langword="false" />. </returns>
    public static bool TryResolve (
        DaemonEditorMode? editorMode,
        out DaemonEditorMode launchEditorMode,
        out ExecutionError? error)
    {
        if (editorMode is null or DaemonEditorMode.Batchmode)
        {
            launchEditorMode = DaemonEditorMode.Batchmode;
            error = null;
            return true;
        }

        if (editorMode == DaemonEditorMode.Gui)
        {
            launchEditorMode = default;
            error = ExecutionError.InternalError(
                "daemon start --editorMode gui is not implemented until GUI Editor attach and launch support is available.",
                UcliCoreErrorCodes.CommandNotImplemented);
            return false;
        }

        launchEditorMode = default;
        error = ExecutionError.InvalidArgument(
            $"daemon start editorMode is invalid. Actual: {editorMode}.");
        return false;
    }
}
