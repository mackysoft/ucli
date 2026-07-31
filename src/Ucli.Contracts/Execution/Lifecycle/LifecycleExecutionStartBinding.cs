using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Carries the durable facts fixed before one Lifecycle Execution may issue its first side effect.
/// </summary>
public sealed record LifecycleExecutionStartBinding
{
    /// <summary> Initializes one validated start binding. </summary>
    /// <param name="lifecycleExecutionRef"> The active or recovery reference resolving to the durable start record. </param>
    /// <param name="project"> The Unity project identity fixed before execution. </param>
    /// <param name="host"> The Editor process and accepted endpoint registration generations. </param>
    /// <param name="startedGeneration"> The complete Editor generation fixed before side effects. </param>
    /// <param name="deadlineUtc"> The immutable execution deadline. </param>
    /// <param name="startedAtUtc"> The execution registration time. </param>
    /// <exception cref="ArgumentNullException"> A required reference is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// The execution reference is not a valid active or recovery Lifecycle Execution reference,
    /// or a timestamp is not UTC.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="deadlineUtc" /> does not follow <paramref name="startedAtUtc" />.
    /// </exception>
    [JsonConstructor]
    public LifecycleExecutionStartBinding (
        ExecutionRef lifecycleExecutionRef,
        UnityProjectIdentity project,
        LifecycleExecutionHostRegistration host,
        UnityEditorGenerationSnapshot startedGeneration,
        DateTimeOffset deadlineUtc,
        DateTimeOffset startedAtUtc)
    {
        LifecycleExecutionContractGuard.RequireReference(
            lifecycleExecutionRef,
            nameof(lifecycleExecutionRef),
            allowTerminal: false);
        if (lifecycleExecutionRef.StatusLocator == null)
        {
            throw new ArgumentException(
                "Lifecycle Execution start reference must resolve to a durable status record.",
                nameof(lifecycleExecutionRef));
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

        LifecycleExecutionRef = lifecycleExecutionRef;
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Host = host ?? throw new ArgumentNullException(nameof(host));
        StartedGeneration = startedGeneration ?? throw new ArgumentNullException(nameof(startedGeneration));
        DeadlineUtc = validatedDeadlineUtc;
        StartedAtUtc = validatedStartedAtUtc;
    }

    /// <summary> Gets the active or recovery reference resolving to the durable start record. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionRef LifecycleExecutionRef { get; private init; }

    /// <summary> Gets the Unity project identity fixed before execution. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityProjectIdentity Project { get; private init; }

    /// <summary> Gets the Editor process and accepted endpoint registration generations. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionHostRegistration Host { get; private init; }

    /// <summary> Gets the complete Editor generation fixed before side effects. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorGenerationSnapshot StartedGeneration { get; private init; }

    /// <summary> Gets the immutable execution deadline. </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset DeadlineUtc { get; private init; }

    /// <summary> Gets the execution registration time. </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset StartedAtUtc { get; private init; }
}
