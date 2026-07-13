using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Represents validated daemon session metadata used at runtime. </summary>
internal sealed record DaemonSession
{
    /// <summary> Initializes validated daemon session metadata. </summary>
    /// <param name="sessionToken"> The IPC authorization token. </param>
    /// <param name="projectFingerprint"> The project fingerprint associated with the session. </param>
    /// <param name="issuedAtUtc"> The UTC timestamp when the session was issued. </param>
    /// <param name="editorMode"> The Unity Editor mode. </param>
    /// <param name="ownerKind"> The session owner kind. </param>
    /// <param name="canShutdownProcess"> Whether uCLI may shut down the process. </param>
    /// <param name="endpoint"> The resolved IPC endpoint. </param>
    /// <param name="processId"> The daemon process identifier when known. </param>
    /// <param name="processStartedAtUtc"> The daemon process start timestamp when known. </param>
    /// <param name="ownerProcessId"> The process identifier that owns the session. </param>
    /// <param name="editorInstanceId"> The Unity Editor instance identifier when known. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when one value or value combination violates the daemon session contract. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a process identifier is not positive. </exception>
    public DaemonSession (
        IpcSessionToken sessionToken,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset issuedAtUtc,
        DaemonEditorMode editorMode,
        DaemonSessionOwnerKind ownerKind,
        bool canShutdownProcess,
        IpcEndpoint endpoint,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        int ownerProcessId,
        Guid? editorInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(sessionToken);
        ArgumentNullException.ThrowIfNull(projectFingerprint);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.Address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ownerProcessId);

        if (issuedAtUtc == default)
        {
            throw new ArgumentException("Daemon session issue timestamp must not be the default value.", nameof(issuedAtUtc));
        }

        if (!Enum.IsDefined(editorMode))
        {
            throw new ArgumentException("Daemon session Editor mode is not defined.", nameof(editorMode));
        }

        if (!Enum.IsDefined(ownerKind))
        {
            throw new ArgumentException("Daemon session owner kind is not defined.", nameof(ownerKind));
        }

        if (!Enum.IsDefined(endpoint.TransportKind))
        {
            throw new ArgumentException("Daemon session endpoint transport kind is not defined.", nameof(endpoint));
        }

        if (processId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Daemon session process identifier must be positive.");
        }

        if (processId.HasValue && !processStartedAtUtc.HasValue)
        {
            throw new ArgumentException(
                "Daemon session process start timestamp must be specified with the process identifier.",
                nameof(processStartedAtUtc));
        }

        if (!processId.HasValue && processStartedAtUtc.HasValue)
        {
            throw new ArgumentException(
                "Daemon session process identifier must be specified with the process start timestamp.",
                nameof(processId));
        }

        if (processStartedAtUtc == default(DateTimeOffset))
        {
            throw new ArgumentException("Daemon session process start timestamp must not be the default value.", nameof(processStartedAtUtc));
        }

        if (editorInstanceId == Guid.Empty)
        {
            throw new ArgumentException("Daemon session Editor instance identifier must not be empty.", nameof(editorInstanceId));
        }

        if (editorMode == DaemonEditorMode.Batchmode
            && (ownerKind != DaemonSessionOwnerKind.Cli || !canShutdownProcess))
        {
            throw new ArgumentException("Batchmode daemon sessions must be CLI-owned and allow process shutdown.", nameof(editorMode));
        }

        if (ownerKind == DaemonSessionOwnerKind.User
            && (editorMode != DaemonEditorMode.Gui || canShutdownProcess))
        {
            throw new ArgumentException("User-owned daemon sessions must run in GUI mode and disallow process shutdown.", nameof(ownerKind));
        }

        if (ownerKind == DaemonSessionOwnerKind.User && !editorInstanceId.HasValue)
        {
            throw new ArgumentException(
                "User-owned daemon sessions must specify an Editor instance identifier.",
                nameof(editorInstanceId));
        }

        SessionToken = sessionToken;
        ProjectFingerprint = projectFingerprint;
        IssuedAtUtc = issuedAtUtc;
        EditorMode = editorMode;
        OwnerKind = ownerKind;
        CanShutdownProcess = canShutdownProcess;
        Endpoint = endpoint;
        ProcessId = processId;
        ProcessStartedAtUtc = processStartedAtUtc;
        OwnerProcessId = ownerProcessId;
        EditorInstanceId = editorInstanceId;
    }

    /// <summary> Gets the IPC authorization token. </summary>
    public IpcSessionToken SessionToken { get; }

    /// <summary> Gets the associated project fingerprint. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the UTC timestamp when the session was issued. </summary>
    public DateTimeOffset IssuedAtUtc { get; }

    /// <summary> Gets the Unity Editor mode. </summary>
    public DaemonEditorMode EditorMode { get; }

    /// <summary> Gets the session owner kind. </summary>
    public DaemonSessionOwnerKind OwnerKind { get; }

    /// <summary> Gets a value indicating whether uCLI may shut down the process. </summary>
    public bool CanShutdownProcess { get; }

    /// <summary> Gets the resolved IPC endpoint. </summary>
    public IpcEndpoint Endpoint { get; }

    /// <summary> Gets the daemon process identifier when known. </summary>
    public int? ProcessId { get; }

    /// <summary> Gets the daemon process start timestamp when known. </summary>
    public DateTimeOffset? ProcessStartedAtUtc { get; }

    /// <summary> Gets the process identifier that owns the session. </summary>
    public int OwnerProcessId { get; }

    /// <summary> Gets the Unity Editor instance identifier when known. </summary>
    public Guid? EditorInstanceId { get; }

}
