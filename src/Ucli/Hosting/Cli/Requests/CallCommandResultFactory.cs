using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Requests.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>call</c> service results. </summary>
internal static class CallCommandResultFactory
{
    /// <summary> Creates one command result for <c>call</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (CallServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Call,
                message: serviceResult.Message,
                payload: CallExecutionPayloadProjector.CreateSuccess(serviceResult.Output!));
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Call,
            serviceResult.Message,
            CallExecutionPayloadProjector.CreateError(serviceResult.Output, serviceResult.Errors),
            serviceResult.Errors);
    }
}
