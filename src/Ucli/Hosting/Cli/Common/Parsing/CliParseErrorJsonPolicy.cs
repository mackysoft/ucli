using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Catalog;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

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

    /// <summary> Creates a JSON invalid-argument result when framework parsing failed before a command handler started. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns> The parse-error result to emit, or <see langword="null" /> when no parse failure was captured. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public static CommandResult? TryCreateParseErrorResult (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (CommandExecutionState.HasStarted || !ParseErrorBuffer.HasAny)
        {
            return null;
        }

        var commandName = UcliCommandMetadataCatalog.ResolveResultCommandName(args);
        var parseErrorMessage = string.Join(" ", ParseErrorBuffer.Messages);
        return CommandResult.InvalidArgument(commandName, parseErrorMessage);
    }
}
