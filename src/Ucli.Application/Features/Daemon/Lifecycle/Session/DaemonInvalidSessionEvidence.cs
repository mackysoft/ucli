using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Carries the untrusted non-secret fields that may make invalid-session cleanup unsafe. </summary>
internal sealed class DaemonInvalidSessionEvidence
{
    /// <summary> Projects non-secret cleanup evidence from a raw persistence contract. </summary>
    /// <param name="contract"> The raw persistence contract. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contract" /> is <see langword="null" />. </exception>
    public DaemonInvalidSessionEvidence (DaemonSessionJsonContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        SchemaVersion = contract.SchemaVersion;
        ProjectFingerprint = contract.ProjectFingerprint;
        EditorMode = contract.EditorMode;
        OwnerKind = contract.OwnerKind;
        CanShutdownProcess = contract.CanShutdownProcess;
        ProcessId = contract.ProcessId;
        ProcessStartedAtUtc = contract.ProcessStartedAtUtc;
        OwnerProcessId = contract.OwnerProcessId;
    }

    /// <summary> Gets the untrusted persisted schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the untrusted project fingerprint. </summary>
    public string? ProjectFingerprint { get; }

    /// <summary> Gets the untrusted Editor mode literal. </summary>
    public string? EditorMode { get; }

    /// <summary> Gets the untrusted owner-kind literal. </summary>
    public string? OwnerKind { get; }

    /// <summary> Gets the untrusted process-shutdown capability flag. </summary>
    public bool CanShutdownProcess { get; }

    /// <summary> Gets the untrusted daemon process identifier. </summary>
    public int? ProcessId { get; }

    /// <summary> Gets the untrusted daemon process start timestamp. </summary>
    public DateTimeOffset? ProcessStartedAtUtc { get; }

    /// <summary> Gets the untrusted owner process identifier. </summary>
    public int? OwnerProcessId { get; }

}
