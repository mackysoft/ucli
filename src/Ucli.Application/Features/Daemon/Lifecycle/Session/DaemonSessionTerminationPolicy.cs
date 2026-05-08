namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Defines when uCLI may terminate a daemon process from session metadata. </summary>
internal static class DaemonSessionTerminationPolicy
{
    /// <summary> Determines whether one daemon session allows uCLI-managed process shutdown. </summary>
    /// <param name="session"> The daemon session metadata. </param>
    /// <returns> <see langword="true" /> when uCLI may request or force process shutdown; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static bool CanShutdownProcess (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.SchemaVersion == DaemonSession.CurrentSchemaVersion
            && DaemonEditorModeCodec.TryParse(session.EditorMode, out _)
            && DaemonSessionOwnerKindCodec.TryParse(session.OwnerKind, out var ownerKind)
            && ownerKind == DaemonSessionOwnerKind.Cli
            && session.CanShutdownProcess
            && session.OwnerProcessId is > 0;
    }

    /// <summary> Determines whether one daemon session should only invalidate endpoint/session artifacts on stop. </summary>
    /// <param name="session"> The daemon session metadata. </param>
    /// <returns><see langword="true" /> when stop should not terminate the Unity process; otherwise <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static bool CanStopEndpointOnly (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.SchemaVersion == DaemonSession.CurrentSchemaVersion
            && DaemonEditorModeCodec.TryParse(session.EditorMode, out var editorMode)
            && editorMode == DaemonEditorMode.Gui
            && DaemonSessionOwnerKindCodec.TryParse(session.OwnerKind, out var ownerKind)
            && (ownerKind == DaemonSessionOwnerKind.User || ownerKind == DaemonSessionOwnerKind.Cli)
            && !session.CanShutdownProcess
            && session.OwnerProcessId is > 0;
    }

    /// <summary> Tries to resolve a process termination target from one daemon session. </summary>
    /// <param name="session"> The daemon session metadata. </param>
    /// <param name="processId"> The target process identifier when resolution succeeds; otherwise default value. </param>
    /// <param name="issuedAtUtc"> The expected issued-at timestamp when resolution succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when a process termination target is safe to use; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static bool TryGetTerminationTarget (
        DaemonSession session,
        out int processId,
        out DateTimeOffset issuedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(session);

        processId = default;
        issuedAtUtc = default;

        if (!CanShutdownProcess(session)
            || session.ProcessId is not int candidateProcessId
            || candidateProcessId <= 0
            || session.IssuedAtUtc == default)
        {
            return false;
        }

        processId = candidateProcessId;
        issuedAtUtc = session.IssuedAtUtc;
        return true;
    }
}
