using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Represents the refresh-start facts retained when a complete refresh result is unavailable. </summary>
public sealed record RefreshLifecycleStartEvidence
{
    /// <summary> Initializes one observed refresh start. </summary>
    /// <param name="startedAtUtc"> The UTC time at which the refresh call began. </param>
    /// <param name="domainReloadGenerationBefore">
    /// The non-negative pre-refresh domain-reload generation, or <see langword="null" /> when it was not observed.
    /// </param>
    /// <exception cref="ArgumentException"> <paramref name="startedAtUtc" /> is not UTC. </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="domainReloadGenerationBefore" /> is negative.
    /// </exception>
    [JsonConstructor]
    public RefreshLifecycleStartEvidence (
        DateTimeOffset startedAtUtc,
        long? domainReloadGenerationBefore)
    {
        StartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            startedAtUtc,
            nameof(startedAtUtc));
        if (domainReloadGenerationBefore is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(domainReloadGenerationBefore),
                domainReloadGenerationBefore,
                "Domain-reload generation must not be negative.");
        }

        DomainReloadGenerationBefore = domainReloadGenerationBefore;
    }

    /// <summary> Gets the UTC time at which the refresh call began. </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset StartedAtUtc { get; private init; }

    /// <summary> Gets the pre-refresh domain-reload generation when it was observed. </summary>
    [JsonInclude]
    [JsonRequired]
    public long? DomainReloadGenerationBefore { get; private init; }
}
