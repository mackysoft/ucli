using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Creates normalized resolve service results. </summary>
internal static class ResolveServiceResultFactory
{
    private const string SuccessMessage = "uCLI resolve completed.";

    private const string FailureMessage = "uCLI resolve failed.";

    /// <summary> Creates one successful resolve result. </summary>
    public static ResolveServiceResult Success (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null)
    {
        return ResolveServiceResult.Success(requestId, opResults, SuccessMessage, readIndex, project, contractViolations);
    }

    /// <summary> Creates one failure result from a structured execution error. </summary>
    public static ResolveServiceResult FromExecutionError (
        Guid requestId,
        ExecutionError error,
        ReadIndexInfo? readIndex = null,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var executionError = ApplicationFailure.FromExecutionError(error);
        return Failure(
            requestId,
            [],
            [
                executionError,
            ],
            readIndex ?? ReadIndexInfoFactory.Unity(fallbackReason: null),
            project);
    }

    /// <summary> Creates one failure result from one IPC error. </summary>
    public static ResolveServiceResult FromIpcError (
        Guid requestId,
        OperationExecutionError error,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        var normalizedError = RequestFailureNormalizer.FromOperationError(error);
        return Failure(
            requestId,
            [],
            [normalizedError],
            readIndex,
            project);
    }

    /// <summary> Creates one failed resolve result. </summary>
    public static ResolveServiceResult Failure (
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null)
    {
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);

        return ResolveServiceResult.Failure(
            requestId,
            opResults,
            errors,
            RequestFailureNormalizer.ResolveMessage(errors, FailureMessage),
            readIndex,
            project,
            contractViolations);
    }
}
