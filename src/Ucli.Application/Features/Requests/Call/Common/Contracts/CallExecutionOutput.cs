using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;

/// <summary> Represents the command payload emitted by one <c>call</c> execution. </summary>
internal sealed record CallExecutionOutput
{
    /// <summary> Initializes the command payload emitted by one <c>call</c> execution. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="project" /> or <paramref name="opResults" /> is <see langword="null" />. </exception>
    public CallExecutionOutput (
        Guid requestId,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        CallPlanOutput? plan,
        IpcExecuteReadPostcondition? readPostcondition,
        OperationExecutionPostReadSource? postReadSource = null)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(opResults);

        RequestId = requestId;
        Project = project;
        OpResults = opResults;
        Plan = plan;
        ReadPostcondition = readPostcondition;
        PostReadSource = postReadSource;
    }

    public Guid RequestId { get; }

    public ProjectIdentityInfo Project { get; init; }

    public IReadOnlyList<OperationExecutionOperationResult> OpResults { get; init; }

    public CallPlanOutput? Plan { get; init; }

    public IpcExecuteReadPostcondition? ReadPostcondition { get; init; }

    public OperationExecutionPostReadSource? PostReadSource { get; init; }

    /// <summary> Gets runtime operation-result violations against published assurance facts. </summary>
    public IReadOnlyList<OperationExecutionContractViolation> ContractViolations { get; init; } = [];
}
