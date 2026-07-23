using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Represents validated daemon session metadata used at runtime. </summary>
internal sealed record DaemonSession
{
    /// <summary> Initializes validated daemon session metadata. </summary>
    /// <param name="sessionGenerationId"> The non-empty identity of this session generation. </param>
    /// <param name="sessionToken"> The IPC authorization token. </param>
    /// <param name="projectFingerprint"> The project fingerprint associated with the session. </param>
    /// <param name="issuedAtUtc"> The UTC timestamp when the session was issued. </param>
    /// <param name="editorMode"> The Unity Editor mode. </param>
    /// <param name="ownerKind"> The session owner kind. </param>
    /// <param name="canShutdownProcess"> Whether uCLI may shut down the process. </param>
    /// <param name="endpointContract"> The IPC endpoint representation retained for persistence and wire boundaries. </param>
    /// <param name="unixSocketEndpointPath">
    /// The guarded Unix-domain-socket path, or <see langword="null" /> for a Named Pipe endpoint.
    /// </param>
    /// <param name="processId"> The daemon process identifier when known. </param>
    /// <param name="processStartedAtUtc"> The daemon process start timestamp when known. </param>
    /// <param name="ownerProcessId"> The process identifier that owns the session. </param>
    /// <param name="editorInstanceId"> The Unity Editor instance identifier when known. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when one value or value combination violates the daemon session contract. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a process identifier is not positive. </exception>
    public DaemonSession (
        Guid sessionGenerationId,
        IpcSessionToken sessionToken,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset issuedAtUtc,
        DaemonEditorMode editorMode,
        DaemonSessionOwnerKind ownerKind,
        bool canShutdownProcess,
        IpcEndpoint endpointContract,
        AbsolutePath? unixSocketEndpointPath,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        int ownerProcessId,
        Guid? editorInstanceId)
    {
        if (sessionGenerationId == Guid.Empty)
        {
            throw new ArgumentException("Daemon session generation identifier must not be empty.", nameof(sessionGenerationId));
        }

        ArgumentNullException.ThrowIfNull(sessionToken);
        ArgumentNullException.ThrowIfNull(projectFingerprint);
        ArgumentNullException.ThrowIfNull(endpointContract);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ownerProcessId);

        IssuedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(issuedAtUtc, nameof(issuedAtUtc));

        if (!Enum.IsDefined(editorMode))
        {
            throw new ArgumentException("Daemon session Editor mode is not defined.", nameof(editorMode));
        }

        if (!Enum.IsDefined(ownerKind))
        {
            throw new ArgumentException("Daemon session owner kind is not defined.", nameof(ownerKind));
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

        ProcessStartedAtUtc = processStartedAtUtc.HasValue
            ? ContractArgumentGuard.RequireUtcTimestamp(processStartedAtUtc.Value, nameof(processStartedAtUtc))
            : null;

        if (editorInstanceId == Guid.Empty)
        {
            throw new ArgumentException("Daemon session Editor instance identifier must not be empty.", nameof(editorInstanceId));
        }

        if (editorMode == DaemonEditorMode.Batchmode && editorInstanceId.HasValue)
        {
            throw new ArgumentException(
                "Batchmode daemon sessions must not specify an Editor instance identifier.",
                nameof(editorInstanceId));
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

        switch (endpointContract.TransportKind)
        {
            case IpcTransportKind.NamedPipe when unixSocketEndpointPath is not null:
                throw new ArgumentException(
                    "Named Pipe daemon sessions must not specify a Unix-domain-socket path.",
                    nameof(unixSocketEndpointPath));

            case IpcTransportKind.UnixDomainSocket when unixSocketEndpointPath is null:
                throw new ArgumentException(
                    "Unix-domain-socket daemon sessions must specify a guarded absolute path.",
                    nameof(unixSocketEndpointPath));

            case IpcTransportKind.UnixDomainSocket when !string.Equals(
                endpointContract.Address,
                unixSocketEndpointPath!.Value,
                StringComparison.Ordinal):
                throw new ArgumentException(
                    "Unix-domain-socket daemon session contract and guarded path must identify the same normalized path.",
                    nameof(unixSocketEndpointPath));
        }

        SessionGenerationId = sessionGenerationId;
        SessionToken = sessionToken;
        ProjectFingerprint = projectFingerprint;
        EditorMode = editorMode;
        OwnerKind = ownerKind;
        CanShutdownProcess = canShutdownProcess;
        EndpointContract = endpointContract;
        UnixSocketEndpointPath = unixSocketEndpointPath;
        ProcessId = processId;
        OwnerProcessId = ownerProcessId;
        EditorInstanceId = editorInstanceId;
    }

    /// <summary> Gets the identity that remains stable for every persisted update of this session generation. </summary>
    public Guid SessionGenerationId { get; }

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

    /// <summary> Gets the IPC endpoint representation used only at persistence and wire boundaries. </summary>
    public IpcEndpoint EndpointContract { get; }

    /// <summary>
    /// Gets the guarded Unix-domain-socket path, or <see langword="null" /> for a Named Pipe endpoint.
    /// </summary>
    public AbsolutePath? UnixSocketEndpointPath { get; }

    /// <summary> Gets the daemon process identifier when known. </summary>
    public int? ProcessId { get; }

    /// <summary> Gets the daemon process start timestamp when known. </summary>
    public DateTimeOffset? ProcessStartedAtUtc { get; }

    /// <summary> Gets the process identifier that owns the session. </summary>
    public int OwnerProcessId { get; }

    /// <summary> Gets the Unity Editor instance identifier when known. </summary>
    public Guid? EditorInstanceId { get; }
}
