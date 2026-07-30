using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from typed-query service results. </summary>
internal static class QueryCommandResultFactory
{
    /// <summary> Creates one command result for a typed-query command. </summary>
    public static CommandResult Create (QueryServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        var startupFailure = StartupFailureFinder.FindInFailures(serviceResult.Errors);
        var payload = new ReadIndexRequestCommandPayload(
            serviceResult.RequestId,
            serviceResult.Project,
            serviceResult.OpResults,
            serviceResult.ContractViolations.Count == 0 ? null : serviceResult.ContractViolations,
            serviceResult.ReadIndex,
            startupFailure?.Startup,
            startupFailure?.Diagnosis,
            startupFailure?.RetryDisposition,
            startupFailure?.SafeToRetryImmediately);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: serviceResult.CommandName,
                message: serviceResult.Message,
                payload: payload);
        }

        return CommandFailureProjector.Create(
            serviceResult.CommandName,
            serviceResult.Message,
            CommandErrorPayload.Detailed(payload),
            serviceResult.Errors);
    }

    /// <summary> Creates one command result for a typed-query command from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (
        Guid requestId,
        string commandName,
        ExecutionError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(error);
        return Create(QueryServiceResultFactory.FromExecutionError(
            commandName,
            requestId,
            error,
            ReadIndexInfoFactory.Unity(fallbackReason: null),
            project: null));
    }
}
