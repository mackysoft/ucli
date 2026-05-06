using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Creates normalized resolve service results. </summary>
internal static class ResolveServiceResultFactory
{
    private const string SuccessMessage = "uCLI resolve completed.";

    private const string FailureMessage = "uCLI resolve failed.";

    /// <summary> Creates one successful resolve result. </summary>
    public static ResolveServiceResult Success (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        ReadIndexInfo readIndex)
    {
        return ResolveServiceResult.Success(requestId, opResults, SuccessMessage, readIndex);
    }

    /// <summary> Creates one failure result from a structured execution error. </summary>
    public static ResolveServiceResult FromExecutionError (
        string requestId,
        ExecutionError error,
        ReadIndexInfo? readIndex = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var executionError = RequestServiceResultPolicy.FromExecutionError(error);
        return Failure(
            requestId,
            [],
            [
                executionError,
            ],
            RequestServiceResultPolicy.ResolveOutcome(error),
            readIndex ?? ReadIndexInfoFactory.Unity(fallbackReason: null));
    }

    /// <summary> Creates one failure result from one IPC error. </summary>
    public static ResolveServiceResult FromIpcError (
        string requestId,
        OperationExecutionError error,
        ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(error);
        var normalizedError = RequestServiceResultPolicy.NormalizeError(error, FailureMessage);
        return Failure(
            requestId,
            [],
            [normalizedError],
            RequestServiceResultPolicy.ResolveOutcome(normalizedError.Code),
            readIndex);
    }

    /// <summary> Creates one failed resolve result. </summary>
    public static ResolveServiceResult Failure (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome,
        ReadIndexInfo readIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);

        return ResolveServiceResult.Failure(
            requestId,
            opResults,
            errors,
            outcome,
            RequestServiceResultPolicy.ResolveFailureMessage(errors, FailureMessage),
            readIndex);
    }
}
