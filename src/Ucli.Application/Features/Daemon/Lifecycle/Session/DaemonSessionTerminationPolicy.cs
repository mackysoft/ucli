using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Defines when uCLI may terminate a daemon process from session metadata. </summary>
internal static class DaemonSessionTerminationPolicy
{
    /// <summary> Represents the daemon stop capability resolved from validated session metadata. </summary>
    public enum StopCapability
    {
        /// <summary> The session does not allow uCLI stop operations. </summary>
        None,

        /// <summary> The session allows endpoint and token invalidation without process termination. </summary>
        EndpointOnly,

        /// <summary> The session allows uCLI-managed process shutdown. </summary>
        ProcessShutdown,
    }

    /// <summary> Resolves the stop capability carried by a validated runtime session. </summary>
    /// <param name="session"> The validated runtime session. </param>
    /// <returns> The stop capability allowed by the session. </returns>
    public static StopCapability ResolveStopCapability (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.OwnerKind == DaemonSessionOwnerKind.Cli && session.CanShutdownProcess)
        {
            return StopCapability.ProcessShutdown;
        }

        if (session.EditorMode == DaemonEditorMode.Gui && !session.CanShutdownProcess)
        {
            return StopCapability.EndpointOnly;
        }

        return StopCapability.None;
    }

    /// <summary> Determines whether one validated session allows uCLI-managed process shutdown. </summary>
    /// <param name="session"> The validated runtime session. </param>
    /// <returns> <see langword="true" /> when uCLI may request or force process shutdown; otherwise <see langword="false" />. </returns>
    public static bool CanShutdownProcess (DaemonSession session)
    {
        return ResolveStopCapability(session) == StopCapability.ProcessShutdown;
    }

    /// <summary> Tries to resolve a process termination target from one validated runtime session. </summary>
    /// <param name="session"> The validated runtime session. </param>
    /// <param name="target"> The safe process termination target when available. </param>
    /// <returns> <see langword="true" /> when a target is available; otherwise <see langword="false" />. </returns>
    public static bool TryGetTerminationTarget (
        DaemonSession session,
        out DaemonProcessTerminationTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);

        target = default;
        if (!CanShutdownProcess(session) || session.ProcessId is not int processId)
        {
            return false;
        }

        target = new DaemonProcessTerminationTarget(processId, session.ProcessStartedAtUtc);
        return true;
    }

    /// <summary> Tries to authorize a termination target from restricted untrusted invalid-session evidence. </summary>
    /// <param name="evidence"> The invalid-session evidence. </param>
    /// <param name="target"> The safe process termination target when all required evidence is valid. </param>
    /// <returns> <see langword="true" /> when the evidence independently proves a safe target; otherwise <see langword="false" />. </returns>
    public static bool TryGetInvalidSessionTerminationTarget (
        DaemonInvalidSessionEvidence evidence,
        out DaemonProcessTerminationTarget target)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        target = default;
        if (evidence.SchemaVersion != DaemonSessionStorageContract.CurrentSchemaVersion
            || evidence.OwnerProcessId is not > 0
            || !ContractLiteralCodec.TryParse<DaemonEditorMode>(evidence.EditorMode, out _)
            || !ContractLiteralCodec.TryParse<DaemonSessionOwnerKind>(evidence.OwnerKind, out var ownerKind)
            || ownerKind != DaemonSessionOwnerKind.Cli
            || !evidence.CanShutdownProcess
            || evidence.ProcessId is not > 0
            || evidence.ProcessStartedAtUtc is not DateTimeOffset processStartedAtUtc
            || processStartedAtUtc == default)
        {
            return false;
        }

        target = new DaemonProcessTerminationTarget(
            evidence.ProcessId.Value,
            processStartedAtUtc);
        return true;
    }
}
