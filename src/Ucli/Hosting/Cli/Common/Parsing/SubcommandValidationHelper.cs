using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Tokens;

namespace MackySoft.Ucli.Hosting.Cli.Common.Parsing;

/// <summary> Provides shared validation for command paths that require one known subcommand token. </summary>
internal static class SubcommandValidationHelper
{
    /// <summary> Creates one invalid-subcommand result when the specified command path arguments are incomplete or unsupported. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <param name="resultCommandName"> The command name emitted in the error envelope. </param>
    /// <param name="commandPath"> The command path shown in the validation message. </param>
    /// <param name="subcommandArgumentIndex"> The index of the subcommand argument to validate. </param>
    /// <param name="supportedSubcommands"> The supported subcommand token list. </param>
    /// <returns>
    /// <para> One error result when the command should stop before framework dispatch. </para>
    /// <para> Otherwise, <see langword="null" />. </para>
    /// </returns>
    /// <exception cref="ArgumentException"> Thrown when a command value is invalid. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="subcommandArgumentIndex" /> is negative. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> or <paramref name="supportedSubcommands" /> is <see langword="null" />. </exception>
    public static CommandResult? TryCreateInvalidSubcommandResult (
        string[] args,
        string resultCommandName,
        string commandPath,
        int subcommandArgumentIndex,
        IReadOnlyList<string> supportedSubcommands)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultCommandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandPath);
        ArgumentNullException.ThrowIfNull(supportedSubcommands);

        if (subcommandArgumentIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(subcommandArgumentIndex), subcommandArgumentIndex, "Subcommand argument index must not be negative.");
        }

        if (args.Length <= subcommandArgumentIndex)
        {
            return CommandResult.InvalidArgument(
                command: resultCommandName,
                message: $"Subcommand is required for command '{commandPath}'. Supported subcommands: {string.Join(", ", supportedSubcommands)}.");
        }

        var subcommand = args[subcommandArgumentIndex];
        if (CommandTokenClassifier.IsHelpOptionToken(subcommand)
            || CommandTokenClassifier.IsVersionOptionToken(subcommand))
        {
            return null;
        }

        for (var i = 0; i < supportedSubcommands.Count; i++)
        {
            if (string.Equals(subcommand, supportedSubcommands[i], StringComparison.Ordinal))
            {
                return null;
            }
        }

        return CommandResult.InvalidArgument(
            command: resultCommandName,
            message: $"Subcommand '{subcommand}' is not recognized for command '{commandPath}'.");
    }

    /// <summary> Creates one invalid-argument result when a leaf subcommand receives an unsupported extra argument. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <param name="commandName"> The validated top-level command name. </param>
    /// <param name="subcommandName"> The leaf subcommand name. </param>
    /// <param name="resultCommandName"> The command name emitted in the error envelope. </param>
    /// <param name="expectedArgumentCount"> The total argument count accepted by the leaf command. </param>
    /// <returns>
    /// <para> One error result when the leaf command should stop before framework dispatch. </para>
    /// <para> Otherwise, <see langword="null" />. </para>
    /// </returns>
    /// <exception cref="ArgumentException"> Thrown when a command name is invalid. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="expectedArgumentCount" /> cannot include command and subcommand tokens. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public static CommandResult? TryCreateUnexpectedLeafArgumentResult (
        string[] args,
        string commandName,
        string subcommandName,
        string resultCommandName,
        int expectedArgumentCount)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subcommandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultCommandName);

        if (expectedArgumentCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedArgumentCount), expectedArgumentCount, "Expected argument count must include command and subcommand tokens.");
        }

        if (args.Length <= expectedArgumentCount
            || args.Length < 2
            || !string.Equals(args[0], commandName, StringComparison.Ordinal)
            || !string.Equals(args[1], subcommandName, StringComparison.Ordinal))
        {
            return null;
        }

        var unexpectedArgument = args[expectedArgumentCount];
        if (CommandTokenClassifier.IsHelpOptionToken(unexpectedArgument)
            || CommandTokenClassifier.IsVersionOptionToken(unexpectedArgument))
        {
            return null;
        }

        return CommandResult.InvalidArgument(
            command: resultCommandName,
            message: $"Argument '{unexpectedArgument}' is not recognized.");
    }
}
