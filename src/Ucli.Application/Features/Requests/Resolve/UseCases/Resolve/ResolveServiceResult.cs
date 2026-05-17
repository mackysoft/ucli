using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents the normalized result returned from one <c>resolve</c> execution workflow. </summary>
internal sealed record ResolveServiceResult
{
    private ResolveServiceResult (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<OperationExecutionContractViolation> contractViolations,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null)
    {
        RequestId = requestId;
        OpResults = opResults;
        ContractViolations = contractViolations;
        Errors = errors;
        Message = message;
        ReadIndex = readIndex;
        Project = project;
    }

    /// <summary> Gets the request identifier associated with this resolve execution. </summary>
    public string RequestId { get; }

    /// <summary> Gets the per-step execution results. </summary>
    public IReadOnlyList<OperationExecutionOperationResult> OpResults { get; }

    /// <summary> Gets runtime contract violations reported by Unity. </summary>
    public IReadOnlyList<OperationExecutionContractViolation> ContractViolations { get; }

    /// <summary> Gets the machine-readable error list. </summary>
    public IReadOnlyList<ApplicationFailure> Errors { get; }

    /// <summary> Gets the application outcome associated with this result. </summary>
    public ApplicationOutcome Outcome => Errors.Count == 0
        ? ApplicationOutcome.Success
        : ApplicationFailureOutcomeResolver.Resolve(Errors);

    /// <summary> Gets the user-facing result message. </summary>
    public string Message { get; }

    /// <summary> Gets the read-index metadata associated with this result. </summary>
    public ReadIndexInfo ReadIndex { get; }

    /// <summary> Gets the resolved Unity project identity when project resolution succeeded. </summary>
    public ProjectIdentityInfo? Project { get; }

    /// <summary> Gets a value indicating whether resolve execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary> Creates one successful resolve result. </summary>
    internal static ResolveServiceResult Success (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        string message,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ResolveServiceResult(
            requestId,
            opResults,
            contractViolations ?? [],
            RequestServiceResultInvariants.EmptyErrors,
            message,
            readIndex,
            project);
    }

    /// <summary> Creates one failed resolve result. </summary>
    internal static ResolveServiceResult Failure (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var failureErrors = RequestServiceResultInvariants.RequireFailureErrors(errors);

        return new ResolveServiceResult(
            requestId,
            opResults,
            contractViolations ?? [],
            failureErrors,
            message,
            readIndex,
            project);
    }
}
