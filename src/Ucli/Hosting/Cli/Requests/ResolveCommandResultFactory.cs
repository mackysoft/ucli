using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>resolve</c> service results. </summary>
internal static class ResolveCommandResultFactory
{
    /// <summary> Creates one command result for <c>resolve</c>. </summary>
    public static CommandResult Create (ResolveServiceResult serviceResult)
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
                command: UcliCommandNames.Resolve,
                message: serviceResult.Message,
                payload: payload);
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Resolve,
            serviceResult.Message,
            CommandErrorPayload.Detailed(payload),
            serviceResult.Errors);
    }

    /// <summary> Creates one command result for <c>resolve</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (
        Guid requestId,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(ResolveServiceResultFactory.FromExecutionError(requestId, error));
    }
}
