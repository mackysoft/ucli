using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Carries the immutable secret-bearing generation used to bootstrap one Unity oneshot host. </summary>
public sealed record IpcOneshotBootstrapEnvelope
{
    /// <summary> Initializes one validated Unity oneshot bootstrap generation. </summary>
    public IpcOneshotBootstrapEnvelope (
        Guid BootstrapId,
        int ParentProcessId,
        DateTimeOffset ParentProcessStartedAtUtc,
        ProjectFingerprint ProjectFingerprint,
        IpcSessionToken SessionToken,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset ExitDeadlineUtc,
        IpcEndpoint Endpoint)
    {
        this.BootstrapId = ContractArgumentGuard.RequireNonEmptyGuid(BootstrapId, nameof(BootstrapId));
        this.ParentProcessId = ContractArgumentGuard.RequirePositive(ParentProcessId, nameof(ParentProcessId));
        this.ParentProcessStartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            ParentProcessStartedAtUtc,
            nameof(ParentProcessStartedAtUtc));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.SessionToken = ContractArgumentGuard.RequireNotNull(SessionToken, nameof(SessionToken));
        this.CreatedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(CreatedAtUtc, nameof(CreatedAtUtc));
        this.ExitDeadlineUtc = ContractArgumentGuard.RequireUtcTimestamp(ExitDeadlineUtc, nameof(ExitDeadlineUtc));
        if (ExitDeadlineUtc <= CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ExitDeadlineUtc),
                ExitDeadlineUtc,
                "Oneshot exit deadline must be later than bootstrap creation time.");
        }

        this.Endpoint = ContractArgumentGuard.RequireNotNull(Endpoint, nameof(Endpoint));
    }

    /// <summary> Gets the non-empty bootstrap generation identifier. </summary>
    public Guid BootstrapId { get; }

    /// <summary> Gets the positive originating CLI process identifier. </summary>
    public int ParentProcessId { get; }

    /// <summary> Gets the originating CLI process start timestamp used to reject identifier reuse. </summary>
    public DateTimeOffset ParentProcessStartedAtUtc { get; }

    /// <summary> Gets the project fingerprint bound to this generation. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the dedicated oneshot session token. </summary>
    public IpcSessionToken SessionToken { get; }

    /// <summary> Gets the UTC timestamp when this generation was created. </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary> Gets the absolute UTC deadline after which this generation is invalid. </summary>
    public DateTimeOffset ExitDeadlineUtc { get; }

    /// <summary> Gets the IPC endpoint bound to this generation. </summary>
    public IpcEndpoint Endpoint { get; }

    /// <inheritdoc />
    public override string ToString () => nameof(IpcOneshotBootstrapEnvelope);
}
