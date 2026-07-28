using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Creates command-level JSON results from Play Mode status execution results. </summary>
internal static class PlayStatusCommandResultFactory
{
    /// <summary> Creates one command result for <c>play status</c>. </summary>
    /// <param name="executionResult"> The Play Mode status execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (PlayStatusExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.PlayStatus,
                message: "uCLI play status retrieval completed.",
                payload: executionResult.Output!);
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.PlayStatus, executionResult.Error!);
    }
}
