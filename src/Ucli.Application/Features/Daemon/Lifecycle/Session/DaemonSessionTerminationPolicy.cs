namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Defines when uCLI may terminate a daemon process from session metadata. </summary>
internal static class DaemonSessionTerminationPolicy
{
    /// <summary> Represents the daemon stop capability resolved from session metadata. </summary>
    public enum StopCapability
    {
        /// <summary> The session does not allow uCLI stop operations. </summary>
        None,

        /// <summary> The session allows endpoint and token invalidation without process termination. </summary>
        EndpointOnly,

        /// <summary> The session allows uCLI-managed process shutdown. </summary>
        ProcessShutdown,
    }

    /// <summary> Resolves the stop capability encoded by one daemon session. </summary>
    /// <param name="session"> The daemon session metadata. </param>
    /// <returns> The stop capability allowed by the session metadata. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static StopCapability ResolveStopCapability (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!TryResolveStopMetadata(session, out var editorMode, out var ownerKind))
        {
            return StopCapability.None;
        }

        if (ownerKind == DaemonSessionOwnerKind.Cli
            && session.CanShutdownProcess)
        {
            return StopCapability.ProcessShutdown;
        }

        if (editorMode == DaemonEditorMode.Gui
            && (ownerKind == DaemonSessionOwnerKind.User || ownerKind == DaemonSessionOwnerKind.Cli)
            && !session.CanShutdownProcess)
        {
            return StopCapability.EndpointOnly;
        }

        return StopCapability.None;
    }

    /// <summary> Determines whether one daemon session allows uCLI-managed process shutdown. </summary>
    /// <param name="session"> The daemon session metadata. </param>
    /// <returns> <see langword="true" /> when uCLI may request or force process shutdown; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static bool CanShutdownProcess (DaemonSession session)
    {
        return ResolveStopCapability(session) == StopCapability.ProcessShutdown;
    }

    /// <summary> Tries to resolve a process termination target from one daemon session. </summary>
    /// <param name="session"> The daemon session metadata. </param>
    /// <param name="target"> The process termination target when resolution succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when a process termination target is safe to use; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static bool TryGetTerminationTarget (
        DaemonSession session,
        out DaemonProcessTerminationTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);

        target = default;

        if (!TryResolveStopMetadata(session, out _, out var ownerKind)
            || ownerKind != DaemonSessionOwnerKind.Cli
            || !session.CanShutdownProcess
            || session.ProcessId is not int candidateProcessId
            || candidateProcessId <= 0)
        {
            return false;
        }

        target = new DaemonProcessTerminationTarget(
            ProcessId: candidateProcessId,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc);
        return true;
    }

    private static bool TryResolveStopMetadata (
        DaemonSession session,
        out DaemonEditorMode editorMode,
        out DaemonSessionOwnerKind ownerKind)
    {
        editorMode = default;
        ownerKind = default;
        return session.SchemaVersion == DaemonSession.CurrentSchemaVersion
            && session.OwnerProcessId is > 0
            && DaemonEditorModeCodec.TryParse(session.EditorMode, out editorMode)
            && DaemonSessionOwnerKindCodec.TryParse(session.OwnerKind, out ownerKind);
    }
}
