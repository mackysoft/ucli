using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Carries untrusted process observations that may require invalid-session cleanup to be skipped. </summary>
internal sealed class DaemonInvalidSessionEvidence
{
    /// <summary> Projects process observations from a raw persistence contract without projecting authorization fields. </summary>
    /// <param name="contract"> The raw persistence contract. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contract" /> is <see langword="null" />. </exception>
    public DaemonInvalidSessionEvidence (DaemonSessionJsonContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        ProcessId = contract.ProcessId;
        ProcessStartedAtUtc = contract.ProcessStartedAtUtc;
    }

    /// <summary> Gets the untrusted daemon process identifier. </summary>
    public int? ProcessId { get; }

    /// <summary> Gets the untrusted daemon process start timestamp. </summary>
    public DateTimeOffset? ProcessStartedAtUtc { get; }

}
