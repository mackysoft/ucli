using MackySoft.Ucli.Application.Features.Status.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Status;

/// <summary> Creates command-level JSON results from status execution results. </summary>
internal static class StatusCommandResultFactory
{
    /// <summary> Creates one command result for <c>status</c>. </summary>
    /// <param name="executionResult"> The status execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (StatusExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Status,
                message: "uCLI status retrieval completed.",
                payload: executionResult.Output!);
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.Status, executionResult.Error!);
    }
}
