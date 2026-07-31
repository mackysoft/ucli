using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>
/// Represents the provider-private request that durably binds one Lifecycle Execution before side effects.
/// </summary>
public sealed record IpcLifecycleExecutionStartRequest
{
    /// <summary> Initializes one validated start-registration request. </summary>
    [JsonConstructor]
    public IpcLifecycleExecutionStartRequest (
        LifecycleExecutionKind kind,
        Guid executionId,
        Sha256Digest definitionDigest,
        DateTimeOffset deadlineUtc,
        DateTimeOffset startedAtUtc)
    {
        _ = new LifecycleExecutionDefinition(kind);
        if (definitionDigest == null)
        {
            throw new ArgumentNullException(nameof(definitionDigest));
        }

        var validatedStartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            startedAtUtc,
            nameof(startedAtUtc));
        var validatedDeadlineUtc = ContractArgumentGuard.RequireUtcTimestamp(
            deadlineUtc,
            nameof(deadlineUtc));
        if (validatedDeadlineUtc <= validatedStartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deadlineUtc),
                deadlineUtc,
                "Lifecycle Execution deadline must follow its start time.");
        }

        Kind = kind;
        ExecutionId = ContractArgumentGuard.RequireNonEmptyGuid(
            executionId,
            nameof(executionId));
        DefinitionDigest = definitionDigest;
        DeadlineUtc = validatedDeadlineUtc;
        StartedAtUtc = validatedStartedAtUtc;
    }

    /// <summary> Gets the fixed action kind. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionKind Kind { get; private init; }

    /// <summary> Gets the caller-issued non-empty execution identifier. </summary>
    [JsonInclude]
    [JsonRequired]
    public Guid ExecutionId { get; private init; }

    /// <summary> Gets the digest of the fixed action definition. </summary>
    [JsonInclude]
    [JsonRequired]
    public Sha256Digest DefinitionDigest { get; private init; }

    /// <summary> Gets the immutable execution deadline. </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset DeadlineUtc { get; private init; }

    /// <summary> Gets the execution registration time selected by the caller. </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset StartedAtUtc { get; private init; }
}
