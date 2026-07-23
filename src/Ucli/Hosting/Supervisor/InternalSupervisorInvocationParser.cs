using MackySoft.FileSystem;

namespace MackySoft.Ucli.Hosting.Supervisor;

/// <summary> Parses the hidden supervisor-host process arguments. </summary>
internal static class InternalSupervisorInvocationParser
{
    /// <summary> Parses one command-line argument vector into an internal supervisor invocation. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns> The parsed invocation state. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public static InternalSupervisorInvocation Parse (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0
            || !string.Equals(args[0], SupervisorConstants.InternalServeFlag, StringComparison.Ordinal))
        {
            return InternalSupervisorInvocation.NotMatched;
        }

        if (args.Length != 3
            || !string.Equals(args[1], SupervisorConstants.RepositoryRootOption, StringComparison.Ordinal)
            || !AbsolutePath.TryParse(args[2], out var repositoryRoot, out _))
        {
            return InternalSupervisorInvocation.Invalid;
        }

        return InternalSupervisorInvocation.Valid(repositoryRoot);
    }
}
