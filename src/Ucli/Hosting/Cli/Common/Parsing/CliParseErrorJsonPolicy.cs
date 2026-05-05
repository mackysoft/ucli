using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;

namespace MackySoft.Ucli.Hosting.Cli.Common.Parsing;

/// <summary> Captures framework parse errors and emits the CLI JSON error envelope when handlers never start. </summary>
internal static class CliParseErrorJsonPolicy
{
    /// <summary> Resets per-invocation parse state and redirects framework parse errors to the buffer. </summary>
    public static void BeginCapture ()
    {
        ParseErrorBuffer.Clear();
        CommandExecutionState.Reset();

        ConsoleApp.LogError = message =>
        {
            ParseErrorBuffer.Add(message);
            Console.Error.WriteLine(message);
        };
    }

    /// <summary> Emits a JSON invalid-argument result when framework parsing failed before a command handler started. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns> <see langword="true" /> when a JSON result was emitted. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public static bool TryEmitParseErrorResult (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (CommandExecutionState.HasStarted || !ParseErrorBuffer.HasAny)
        {
            return false;
        }

        var commandName = UcliCommandCatalog.ResolveResultCommandName(args);
        var parseErrorMessage = string.Join(" ", ParseErrorBuffer.Messages);
        var parseErrorResult = CommandResult.InvalidArgument(commandName, parseErrorMessage);
        CommandResultWriter.WriteToStandardOutput(parseErrorResult);
        Environment.ExitCode = parseErrorResult.ExitCode;
        return true;
    }
}
