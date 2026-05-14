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
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null)
    {
        return ResolveServiceResult.Success(requestId, opResults, SuccessMessage, readIndex, project ?? ProjectIdentityInfo.Unknown);
    }

    /// <summary> Creates one failure result from a structured execution error. </summary>
    public static ResolveServiceResult FromExecutionError (
        string requestId,
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
        string requestId,
        OperationExecutionError error,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        var normalizedError = RequestFailureNormalizer.FromOperationError(error, FailureMessage);
        return Failure(
            requestId,
            [],
            [normalizedError],
            readIndex,
            project);
    }

    /// <summary> Creates one failed resolve result. </summary>
    public static ResolveServiceResult Failure (
        string requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);

        return ResolveServiceResult.Failure(
            requestId,
            opResults,
            errors,
            RequestFailureNormalizer.ResolveMessage(errors, FailureMessage),
            readIndex,
            project);
    }
}
