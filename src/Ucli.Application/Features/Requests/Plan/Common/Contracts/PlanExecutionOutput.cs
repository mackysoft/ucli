namespace MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;

/// <summary> Represents the command payload emitted by one <c>plan</c> execution. </summary>
internal sealed record PlanExecutionOutput
{
    /// <summary> Initializes the command payload emitted by one <c>plan</c> execution. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="project" />, <paramref name="opResults" />, or <paramref name="readIndex" /> is <see langword="null" />. </exception>
    public PlanExecutionOutput (
        Guid requestId,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        ReadIndexInfo readIndex,
        string? planToken)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);

        RequestId = requestId;
        Project = project;
        OpResults = opResults;
        ReadIndex = readIndex;
        PlanToken = planToken;
    }

    public Guid RequestId { get; }

    public ProjectIdentityInfo Project { get; init; }

    public IReadOnlyList<OperationExecutionOperationResult> OpResults { get; init; }

    public ReadIndexInfo ReadIndex { get; init; }

    public string? PlanToken { get; init; }

    /// <summary> Gets runtime operation-result violations against published assurance facts. </summary>
    public IReadOnlyList<OperationExecutionContractViolation> ContractViolations { get; init; } = [];
}
