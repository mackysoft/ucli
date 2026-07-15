using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Represents the normalized result returned from one fixed operation execution workflow. </summary>
internal sealed record OperationExecuteResult
{
    private OperationExecuteResult (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations,
        IpcExecuteReadPostcondition? readPostcondition,
        OperationExecutionPostReadSource? postReadSource,
        ProjectIdentityInfo? project)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        RequestId = requestId;
        OpResults = opResults;
        Errors = errors;
        Message = message;
        ContractViolations = contractViolations ?? [];
        ReadPostcondition = readPostcondition;
        PostReadSource = postReadSource;
        Project = project;
    }

    /// <summary> Gets the request identifier associated with this execution. </summary>
    public Guid RequestId { get; }

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

    /// <summary> Gets runtime operation-result violations against published assurance facts. </summary>
    public IReadOnlyList<OperationExecutionContractViolation> ContractViolations { get; }

    /// <summary> Gets the read postcondition emitted by mutation execution, when available. </summary>
    public IpcExecuteReadPostcondition? ReadPostcondition { get; }

    /// <summary> Gets source facts used by post-read verification, when available. </summary>
    public OperationExecutionPostReadSource? PostReadSource { get; }

    /// <summary> Gets the resolved Unity project identity when project resolution succeeded. </summary>
    public ProjectIdentityInfo? Project { get; }

    /// <summary> Gets a value indicating whether the operation execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary> Creates one successful operation execution result. </summary>
    internal static OperationExecuteResult Success (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        string message,
        IpcExecuteReadPostcondition? readPostcondition,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null,
        OperationExecutionPostReadSource? postReadSource = null)
    {
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(project);

        return new OperationExecuteResult(
            requestId,
            opResults,
            RequestServiceResultInvariants.EmptyErrors,
            message,
            contractViolations,
            readPostcondition,
            postReadSource,
            project);
    }

    /// <summary> Creates one failed operation execution result. </summary>
    internal static OperationExecuteResult Failure (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null,
        IpcExecuteReadPostcondition? readPostcondition = null,
        ProjectIdentityInfo? project = null,
        OperationExecutionPostReadSource? postReadSource = null)
    {
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var failureErrors = RequestServiceResultInvariants.RequireFailureErrors(errors);

        return new OperationExecuteResult(
            requestId,
            opResults,
            failureErrors,
            message,
            contractViolations,
            readPostcondition,
            postReadSource,
            project);
    }
}
