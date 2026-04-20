using MackySoft.Ucli.Features.Init.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli;

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
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.Init,
                message: "uCLI config template generation completed.",
                payload: new
                {
                    configPath = output.ConfigPath,
                    gitignorePath = output.GitIgnorePath,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.Init, executionResult.Error!);
    }
}