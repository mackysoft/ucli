using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Represents the provider-independent typed result owned by one refresh Lifecycle Execution.
/// </summary>
public sealed record RefreshLifecycleResult
{
    /// <summary> Initializes a completed project-refresh result. </summary>
    /// <param name="refresh"> The observed AssetDatabase refresh facts. </param>
    /// <param name="lifecycle"> The complete lifecycle observed after refresh recovery. </param>
    /// <param name="readPostcondition"> The optional safety requirements for invalidated read surfaces. </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="refresh" /> or <paramref name="lifecycle" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The final lifecycle domain-reload generation does not match the refresh evidence.
    /// </exception>
    [JsonConstructor]
    public RefreshLifecycleResult (
        RefreshEvidence refresh,
        UnityEditorObservation lifecycle,
        ExecutionReadPostcondition? readPostcondition)
    {
        Refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        if (lifecycle.State.Generations.DomainReloadGeneration
            != refresh.DomainReloadGenerationAfter)
        {
            throw new ArgumentException(
                "Final lifecycle domain-reload generation must match the completed refresh evidence.",
                nameof(lifecycle));
        }

        ReadPostcondition = readPostcondition;
    }

    /// <summary> Gets the observed AssetDatabase refresh facts. </summary>
    [JsonInclude]
    [JsonRequired]
    public RefreshEvidence Refresh { get; private init; }

    /// <summary> Gets the complete lifecycle observed after refresh recovery. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorObservation Lifecycle { get; private init; }

    /// <summary> Gets the optional safety requirements for invalidated read surfaces. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExecutionReadPostcondition? ReadPostcondition { get; }

    /// <summary> Represents the complete AssetDatabase refresh observation. </summary>
    public sealed record RefreshEvidence
    {
        /// <summary> Initializes validated refresh evidence. </summary>
        /// <param name="startedAtUtc"> The UTC time at which AssetDatabase refresh began. </param>
        /// <param name="completedAtUtc"> The UTC time at which refresh recovery was completely observed. </param>
        /// <param name="domainReloadGenerationBefore"> The non-negative domain-reload generation before refresh. </param>
        /// <param name="domainReloadGenerationAfter"> The non-negative domain-reload generation after refresh. </param>
        /// <exception cref="ArgumentException"> A timestamp is not UTC. </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// A generation is negative, a generation regresses, or completion precedes the start.
        /// </exception>
        [JsonConstructor]
        public RefreshEvidence (
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc,
            long domainReloadGenerationBefore,
            long domainReloadGenerationAfter)
        {
            StartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
                startedAtUtc,
                nameof(startedAtUtc));
            CompletedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
                completedAtUtc,
                nameof(completedAtUtc));
            if (CompletedAtUtc < StartedAtUtc)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedAtUtc),
                    completedAtUtc,
                    "Refresh completion must not precede its start.");
            }

            if (domainReloadGenerationBefore < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(domainReloadGenerationBefore),
                    domainReloadGenerationBefore,
                    "Domain-reload generation must not be negative.");
            }
            if (domainReloadGenerationAfter < domainReloadGenerationBefore)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(domainReloadGenerationAfter),
                    domainReloadGenerationAfter,
                    "Domain-reload generation must not regress during refresh.");
            }

            DomainReloadGenerationBefore = domainReloadGenerationBefore;
            DomainReloadGenerationAfter = domainReloadGenerationAfter;
        }

        /// <summary> Gets the UTC time at which AssetDatabase refresh began. </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset StartedAtUtc { get; private init; }

        /// <summary> Gets the UTC time at which refresh recovery was completely observed. </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset CompletedAtUtc { get; private init; }

        /// <summary> Gets the domain-reload generation observed before refresh. </summary>
        [JsonInclude]
        [JsonRequired]
        public long DomainReloadGenerationBefore { get; private init; }

        /// <summary> Gets the domain-reload generation observed after refresh. </summary>
        [JsonInclude]
        [JsonRequired]
        public long DomainReloadGenerationAfter { get; private init; }
    }
}
