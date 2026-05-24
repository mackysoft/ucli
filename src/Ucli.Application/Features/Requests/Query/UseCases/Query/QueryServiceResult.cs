using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents the normalized result returned from one typed-query execution workflow. </summary>
internal sealed record QueryServiceResult
{
    private QueryServiceResult (
        string commandName,
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null)
    {
        CommandName = commandName;
        RequestId = requestId;
        OpResults = opResults;
        Errors = errors;
        Message = message;
        ReadIndex = readIndex;
        ContractViolations = contractViolations ?? [];
        Project = project;
    }

    /// <summary> Gets the command name associated with this typed-query result. </summary>
    public string CommandName { get; }

    /// <summary> Gets the request identifier associated with this query execution. </summary>
    public string RequestId { get; }

    /// <summary> Gets the per-step execution results. </summary>
    public IReadOnlyList<OperationExecutionOperationResult> OpResults { get; }

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

    /// <summary> Gets runtime operation-result violations against published assurance facts. </summary>
    public IReadOnlyList<OperationExecutionContractViolation> ContractViolations { get; }

    /// <summary> Gets the resolved Unity project identity when project resolution succeeded. </summary>
    public ProjectIdentityInfo? Project { get; }

    /// <summary> Gets a value indicating whether query execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary> Creates one successful typed-query result. </summary>
    internal static QueryServiceResult Success (
        string commandName,
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        string message,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new QueryServiceResult(
            commandName,
            requestId,
            opResults,
            RequestServiceResultInvariants.EmptyErrors,
            message,
            readIndex,
            contractViolations: contractViolations,
            project: project);
    }

    /// <summary> Creates one failed typed-query result. </summary>
    internal static QueryServiceResult Failure (
        string commandName,
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var failureErrors = RequestServiceResultInvariants.RequireFailureErrors(errors);

        return new QueryServiceResult(
            commandName,
            requestId,
            opResults,
            failureErrors,
            message,
            readIndex,
            contractViolations: contractViolations,
            project: project);
    }
}
