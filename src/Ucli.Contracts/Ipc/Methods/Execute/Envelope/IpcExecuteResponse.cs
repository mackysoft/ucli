using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an <c>execute</c> IPC response payload with its resolved Unity project identity. </summary>
public sealed record IpcExecuteResponse
{
    /// <summary> Initializes an <c>execute</c> IPC response. </summary>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="project"> The resolved Unity project identity for the request. </param>
    /// <param name="planToken"> The optional plan token issued by the <c>plan</c> command. </param>
    /// <param name="readPostcondition"> The optional mutation-to-read postcondition contract. </param>
    /// <param name="postReadSource"> The optional source facts aligned with <paramref name="opResults" />. </param>
    /// <param name="contractViolations"> The optional runtime contract violations. </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="opResults" /> or <paramref name="project" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="planToken" /> is empty, or when <paramref name="postReadSource" /> or <paramref name="contractViolations" /> does not match <paramref name="opResults" />.
    /// </exception>
    [JsonConstructor]
    public IpcExecuteResponse (
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IpcProjectIdentity project,
        string? planToken,
        IpcExecuteReadPostcondition? readPostcondition,
        IpcExecutePostReadSource? postReadSource,
        IReadOnlyList<IpcExecuteContractViolation>? contractViolations)
    {
        var opResultSnapshot = ContractArgumentGuard.RequireItems(opResults, nameof(opResults));
        var contractViolationSnapshot = contractViolations == null
            ? null
            : ContractArgumentGuard.RequireItems(contractViolations, nameof(contractViolations));
        if (postReadSource != null || contractViolationSnapshot is { Count: > 0 })
        {
            var operationById = new Dictionary<IpcExecuteStepId, string>(opResultSnapshot.Count);
            for (var index = 0; index < opResultSnapshot.Count; index++)
            {
                var opResult = opResultSnapshot[index];
                if (!operationById.TryAdd(opResult.OpId, opResult.Op))
                {
                    throw new ArgumentException($"The 'opResults[{index}].opId' value is duplicated.", nameof(opResults));
                }
            }

            if (postReadSource != null)
            {
                if (postReadSource.Steps.Count != opResultSnapshot.Count)
                {
                    throw new ArgumentException("The 'postReadSource.steps' entries must correspond one-to-one with 'opResults'.", nameof(postReadSource));
                }

                var sourceIds = new HashSet<IpcExecuteStepId>();
                for (var index = 0; index < postReadSource.Steps.Count; index++)
                {
                    var sourceStep = postReadSource.Steps[index];
                    if (!operationById.TryGetValue(sourceStep.OpId, out var operationName))
                    {
                        throw new ArgumentException($"The 'postReadSource.steps[{index}].opId' value does not match 'opResults'.", nameof(postReadSource));
                    }

                    if (!sourceIds.Add(sourceStep.OpId))
                    {
                        throw new ArgumentException($"The 'postReadSource.steps[{index}].opId' value is duplicated.", nameof(postReadSource));
                    }

                    if (!IpcExecutePostReadSourceRules.IsCompatibleWithOperation(
                            operationName,
                            sourceStep.SourceKind,
                            sourceStep.PlayModeMutation,
                            sourceStep.Commit,
                            sourceStep.PersistenceExpected,
                            sourceStep.ExpectedPostState))
                    {
                        throw new ArgumentException($"The 'postReadSource.steps[{index}]' source facts do not match 'opResults'.", nameof(postReadSource));
                    }
                }
            }

            if (contractViolationSnapshot != null)
            {
                for (var index = 0; index < contractViolationSnapshot.Count; index++)
                {
                    var violation = contractViolationSnapshot[index];
                    if (!operationById.TryGetValue(violation.OpId, out var operationName)
                        || !string.Equals(violation.Operation, operationName, StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"The 'contractViolations[{index}]' identity does not match 'opResults'.", nameof(contractViolations));
                    }
                }
            }
        }

        OpResults = opResultSnapshot;
        Project = project ?? throw new ArgumentNullException(nameof(project), "The 'project' field is required.");
        PlanToken = planToken == null
            ? null
            : ContractArgumentGuard.RequireValue(planToken, nameof(planToken));
        ReadPostcondition = readPostcondition;
        PostReadSource = postReadSource;
        ContractViolations = contractViolationSnapshot;
    }

    /// <summary> Gets the per-step execution results. </summary>
    public IReadOnlyList<IpcExecuteOperationResult> OpResults { get; }

    /// <summary> Gets the resolved Unity project identity for the request. </summary>
    public IpcProjectIdentity Project { get; }

    /// <summary> Gets the optional plan token issued by the <c>plan</c> command. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlanToken { get; }

    /// <summary> Gets the optional mutation-to-read postcondition contract emitted after call execution. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcExecuteReadPostcondition? ReadPostcondition { get; }

    /// <summary> Gets source facts needed to verify post-read claims from this portable result. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcExecutePostReadSource? PostReadSource { get; }

    /// <summary> Gets runtime result violations against published operation assurance facts. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<IpcExecuteContractViolation>? ContractViolations { get; }
}
