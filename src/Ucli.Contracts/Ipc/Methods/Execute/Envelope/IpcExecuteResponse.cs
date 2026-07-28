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
            if (postReadSource != null)
            {
                if (postReadSource.Steps.Count != opResultSnapshot.Count)
                {
                    throw new ArgumentException("The 'postReadSource.steps' entries must correspond one-to-one with 'opResults'.", nameof(postReadSource));
                }

                for (var index = 0; index < postReadSource.Steps.Count; index++)
                {
                    var sourceStep = postReadSource.Steps[index];
                    if (!IpcExecutePostReadSourceRules.IsCompatibleWithOperation(
                            opResultSnapshot[index].Op,
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
                    var matchesOperation = false;
                    for (var opResultIndex = 0; opResultIndex < opResultSnapshot.Count; opResultIndex++)
                    {
                        if (string.Equals(
                                violation.InstancePath,
                                $"/opResults/{opResultIndex}",
                                StringComparison.Ordinal)
                            && string.Equals(
                                violation.Operation,
                                opResultSnapshot[opResultIndex].Op,
                                StringComparison.Ordinal))
                        {
                            matchesOperation = true;
                            break;
                        }
                    }

                    if (!matchesOperation)
                    {
                        throw new ArgumentException(
                            $"The 'contractViolations[{index}]' instance path and operation do not match 'opResults'.",
                            nameof(contractViolations));
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
