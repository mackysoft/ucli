using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Init;

/// <summary> Creates command-level JSON results from init execution results. </summary>
internal static class InitCommandResultFactory
{
    /// <summary> Creates one command result for <c>init</c>. </summary>
    /// <param name="executionResult"> The init execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (InitExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Init,
                message: "uCLI config template generation completed.",
                payload: executionResult.Output!);
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.Init, executionResult.Error!);
    }
}
