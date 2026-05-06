using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Writes CLI command results to the process output stream. </summary>
internal interface ICommandResultWriter
{
    /// <summary> Writes one command result to standard output. </summary>
    /// <param name="result"> The command result. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> is <see langword="null" />. </exception>
    void WriteToStandardOutput (CommandResult result);
}
