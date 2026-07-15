using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Identifies the daemon session generation permitted to wait through one bounded domain reload. </summary>
internal sealed record DaemonLifecycleRecoveryLease
{
    /// <summary> Initializes one bounded domain-reload recovery lease. </summary>
    /// <param name="sessionGenerationId"> The non-empty daemon session generation that owned the endpoint before reload. </param>
    /// <param name="expiresAtUtc"> The explicit UTC deadline after which the endpoint gap is no longer recoverable. </param>
    /// <exception cref="ArgumentException"> Thrown when the identifier is empty, or when the expiration is unspecified or has a non-UTC offset. </exception>
    [JsonConstructor]
    public DaemonLifecycleRecoveryLease (
        Guid sessionGenerationId,
        DateTimeOffset expiresAtUtc)
    {
        SessionGenerationId = ContractArgumentGuard.RequireNonEmptyGuid(
            sessionGenerationId,
            nameof(sessionGenerationId));
        ExpiresAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            expiresAtUtc,
            nameof(expiresAtUtc));
    }

    /// <summary> Gets the daemon session generation that owned the endpoint before reload. </summary>
    public Guid SessionGenerationId { get; }

    /// <summary> Gets the UTC deadline after which the endpoint gap is no longer recoverable. </summary>
    public DateTimeOffset ExpiresAtUtc { get; }
}
