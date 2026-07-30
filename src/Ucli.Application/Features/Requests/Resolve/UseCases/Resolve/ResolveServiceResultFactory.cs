using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Creates normalized resolve service results. </summary>
internal static class ResolveServiceResultFactory
{
    private const string SuccessMessage = "uCLI resolve completed.";

    /// <summary> Creates one successful resolve result. </summary>
    public static ResolveServiceResult Success (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionContractViolation> contractViolations)
    {
        return ResolveServiceResult.Success(requestId, opResults, SuccessMessage, readIndex, project, contractViolations);
    }

    /// <summary> Creates one failure result from a structured execution error. </summary>
    public static ResolveServiceResult FromExecutionError (
        Guid requestId,
        ExecutionError error,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project)
    {
        ArgumentNullException.ThrowIfNull(error);

        var executionError = ApplicationFailure.FromExecutionError(error);
        return Failure(
            requestId,
            [],
            [
                executionError,
            ],
            readIndex,
            project,
            contractViolations: []);
    }

    /// <summary> Creates one failed resolve result. </summary>
    public static ResolveServiceResult Failure (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project,
        IReadOnlyList<OperationExecutionContractViolation> contractViolations)
    {
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(contractViolations);
        var failureErrors = RequestServiceResultInvariants.RequireFailureErrors(errors);

        return ResolveServiceResult.Failure(
            requestId,
            opResults,
            failureErrors,
            failureErrors[0].Message,
            readIndex,
            project,
            contractViolations);
    }
}
