using MackySoft.Ucli.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Testing;

/// <summary> Creates command-level JSON results from test-profile initialization execution results. </summary>
internal static class TestProfileInitCommandResultFactory
{
    /// <summary> Creates one command result for <c>test profile init</c>. </summary>
    /// <param name="executionResult"> The test-profile initialization execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (TestProfileInitExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.TestProfileInit,
                message: "Test profile template initialization completed.",
                payload: new
                {
                    profilePath = output.ProfilePath,
                });
        }

        return CommandResultFactory.FromExecutionError(
            UcliCommandNames.TestProfileInit,
            executionResult.Error!);
    }
}