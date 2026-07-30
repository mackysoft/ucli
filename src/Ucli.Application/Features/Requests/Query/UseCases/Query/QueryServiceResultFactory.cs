using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Creates normalized typed-query service results. </summary>
internal static class QueryServiceResultFactory
{
    private const string SuccessMessage = "uCLI query completed.";

    /// <summary> Creates one successful typed-query result. </summary>
    public static QueryServiceResult Success (
        string commandName,
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo project,
        IReadOnlyList<OperationExecutionContractViolation> contractViolations)
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
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project)
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
            readIndex,
            project,
            contractViolations: []);
    }

    /// <summary> Creates one failed typed-query result. </summary>
    public static QueryServiceResult Failure (
        string commandName,
        Guid requestId,
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        ReadIndexInfo readIndex,
        ProjectIdentityInfo? project,
        IReadOnlyList<OperationExecutionContractViolation> contractViolations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(contractViolations);

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
