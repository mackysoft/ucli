using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>eval</c> service results. </summary>
internal static class EvalCommandResultFactory
{
    private const string SuccessMessage = "uCLI eval completed.";

    /// <summary> Creates one command result for <c>eval</c>. </summary>
    /// <param name="serviceResult"> The call workflow result used by eval. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (CallServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        var payload = CallExecutionPayloadProjector.Create(serviceResult.Output);
        if (!serviceResult.IsSuccess)
        {
            StartupFailurePayloadProjector.AppendFromFailures(payload, serviceResult.Errors);
        }

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Eval,
                message: SuccessMessage,
                payload: payload);
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Eval,
            serviceResult.Message,
            payload,
            serviceResult.Errors);
    }
}
