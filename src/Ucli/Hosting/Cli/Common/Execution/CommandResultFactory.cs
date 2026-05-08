using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Creates command results from structured execution errors. </summary>
internal static class CommandResultFactory
{
    /// <summary> Creates an error command result from one execution error. </summary>
    /// <param name="command"> The command name written to the result. </param>
    /// <param name="error"> The execution error to map. </param>
    /// <returns> The mapped command result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="error" /> kind is unsupported. </exception>
    public static CommandResult FromExecutionError (
        string command,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return CommandFailureProjector.Create(command, ApplicationFailure.FromExecutionError(error));
    }
}
