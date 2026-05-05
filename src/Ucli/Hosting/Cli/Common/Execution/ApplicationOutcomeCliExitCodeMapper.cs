using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Maps application outcomes to CLI process exit codes. </summary>
internal static class ApplicationOutcomeCliExitCodeMapper
{
    /// <summary> Converts one application outcome to the corresponding CLI exit code. </summary>
    /// <param name="outcome"> The application outcome. </param>
    /// <returns> The command-line exit code. </returns>
    public static int ToExitCode (ApplicationOutcome outcome)
    {
        return outcome switch
        {
            ApplicationOutcome.Success => 0,
            ApplicationOutcome.TestFailure => 1,
            ApplicationOutcome.InfrastructureError => 2,
            ApplicationOutcome.InvalidArgument => (int)CliExitCode.InvalidArgument,
            ApplicationOutcome.ToolError => (int)CliExitCode.ToolError,
            _ => (int)CliExitCode.ToolError,
        };
    }
}
