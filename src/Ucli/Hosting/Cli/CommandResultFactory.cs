using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli;

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

        return error.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => CommandResult.InvalidArgument(command, error.Message),
            ExecutionErrorKind.Timeout => CommandResult.Timeout(command, error.Message),
            ExecutionErrorKind.InternalError => CommandResult.InternalError(command, error.Message),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Kind, "Unsupported execution error kind."),
        };
    }
}