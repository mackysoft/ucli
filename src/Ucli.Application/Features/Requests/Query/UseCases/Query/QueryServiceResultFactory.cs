using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Creates normalized typed-query service results. </summary>
internal static class QueryServiceResultFactory
{
    private const string SuccessMessage = "uCLI query completed.";

    private const string FailureMessage = "uCLI query failed.";

    /// <summary> Creates one successful typed-query result. </summary>
    public static QueryServiceResult Success (
        string commandName,
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null)
    {
        return QueryServiceResult.Success(
            commandName,
            requestId,
            opResults,
            SuccessMessage,
            readIndex,
            project,
            contractViolations);
    }

    /// <summary> Creates one failure result from a structured execution error. </summary>
    public static QueryServiceResult FromExecutionError (
        string commandName,
        Guid requestId,
        ExecutionError error,
        ReadIndexInfo? readIndex = null,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var executionError = ApplicationFailure.FromExecutionError(error);
        return Failure(
            commandName,
            requestId,
            [],
            [
                executionError,
            ],
            error.Message,
            readIndex ?? ReadIndexInfoFactory.Unity(fallbackReason: null),
            project);
    }

    /// <summary> Creates one failure result from one IPC error. </summary>
    public static QueryServiceResult FromIpcError (
        string commandName,
        Guid requestId,
        OperationExecutionError error,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        var normalizedError = RequestFailureNormalizer.FromOperationError(error, FailureMessage);
        return Failure(
            commandName,
            requestId,
            [],
            [normalizedError],
            normalizedError.Message,
            readIndex,
            project);
    }

    /// <summary> Creates one failed typed-query result. </summary>
    public static QueryServiceResult Failure (
        string commandName,
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project = null,
        IReadOnlyList<OperationExecutionContractViolation>? contractViolations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);

        return QueryServiceResult.Failure(
            commandName,
            requestId,
            opResults,
            errors,
            message,
            readIndex,
            project,
            contractViolations);
    }
}
