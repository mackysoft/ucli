using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Represents the normalized result returned from one fixed operation execution workflow. </summary>
internal sealed record OperationExecuteResult
{
    private OperationExecuteResult (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        OperationExecutionReadPostcondition? readPostcondition,
        ProjectIdentityInfo? project)
    {
        RequestId = requestId;
        OpResults = opResults;
        Errors = errors;
        Message = message;
        ReadPostcondition = readPostcondition;
        Project = project;
    }

    /// <summary> Gets the request identifier associated with this execution. </summary>
    public string RequestId { get; }

    /// <summary> Gets the per-step execution results. </summary>
    public IReadOnlyList<OperationExecutionOperationResult> OpResults { get; }

    /// <summary> Gets the machine-readable error list. </summary>
    public IReadOnlyList<ApplicationFailure> Errors { get; }

    /// <summary> Gets the application outcome associated with this response. </summary>
    public ApplicationOutcome Outcome => Errors.Count == 0
        ? ApplicationOutcome.Success
        : ApplicationFailureOutcomeResolver.Resolve(Errors);

    /// <summary> Gets the user-facing result message. </summary>
    public string Message { get; }

    /// <summary> Gets the read postcondition emitted by mutation execution, when available. </summary>
    public OperationExecutionReadPostcondition? ReadPostcondition { get; }

    /// <summary> Gets the resolved Unity project identity when project resolution succeeded. </summary>
    public ProjectIdentityInfo? Project { get; }

    /// <summary> Gets a value indicating whether the operation execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary> Creates one successful operation execution result. </summary>
    internal static OperationExecuteResult Success (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        string message,
        OperationExecutionReadPostcondition? readPostcondition,
        ProjectIdentityInfo project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(project);

        return new OperationExecuteResult(
            requestId,
            opResults,
            RequestServiceResultInvariants.EmptyErrors,
            message,
            readPostcondition,
            project);
    }

    /// <summary> Creates one failed operation execution result. </summary>
    internal static OperationExecuteResult Failure (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        OperationExecutionReadPostcondition? readPostcondition = null,
        ProjectIdentityInfo? project = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var failureErrors = RequestServiceResultInvariants.RequireFailureErrors(errors);

        return new OperationExecuteResult(
            requestId,
            opResults,
            failureErrors,
            message,
            readPostcondition,
            project);
    }
}
