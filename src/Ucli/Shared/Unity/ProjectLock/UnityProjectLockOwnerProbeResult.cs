namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Represents the observed owner of an existing Unity project lock file. </summary>
/// <param name="Status"> The owner probe status. </param>
/// <param name="ProcessId"> The active owner process identifier when known. </param>
/// <param name="Message"> A diagnostic ownership message when available. </param>
internal sealed record UnityProjectLockOwnerProbeResult (
    UnityProjectLockOwnerProbeStatus Status,
    int? ProcessId,
    string? Message)
{
    /// <summary> Creates a no-owner result. </summary>
    /// <returns> The owner probe result. </returns>
    public static UnityProjectLockOwnerProbeResult NoOwner ()
    {
        return new UnityProjectLockOwnerProbeResult(UnityProjectLockOwnerProbeStatus.NoOwner, null, null);
    }

    /// <summary> Creates an active-owner result. </summary>
    /// <param name="processId"> The owner process identifier when known. </param>
    /// <param name="message"> The owner diagnostic message. </param>
    /// <returns> The owner probe result. </returns>
    public static UnityProjectLockOwnerProbeResult ActiveOwner (
        int? processId,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new UnityProjectLockOwnerProbeResult(UnityProjectLockOwnerProbeStatus.ActiveOwner, processId, message);
    }

    /// <summary> Creates an ambiguous result. </summary>
    /// <param name="message"> The ambiguity diagnostic message. </param>
    /// <returns> The owner probe result. </returns>
    public static UnityProjectLockOwnerProbeResult Ambiguous (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new UnityProjectLockOwnerProbeResult(UnityProjectLockOwnerProbeStatus.Ambiguous, null, message);
    }
}
