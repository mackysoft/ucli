using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Parsing;

/// <summary> Provides shared validation for top-level commands that require one known subcommand token. </summary>
internal static class SubcommandValidationHelper
{
    /// <summary> Creates one invalid-subcommand result when the specified command arguments are incomplete or unsupported. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <param name="commandName"> The validated top-level command name. </param>
    /// <param name="supportedSubcommands"> The supported subcommand token list. </param>
    /// <returns>
    /// <para> One error result when the command should stop before framework dispatch. </para>
    /// <para> Otherwise, <see langword="null" />. </para>
    /// </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="commandName" /> is invalid. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> or <paramref name="supportedSubcommands" /> is <see langword="null" />. </exception>
    public static CommandResult? TryCreateInvalidSubcommandResult (
        string[] args,
        string commandName,
        IReadOnlyList<string> supportedSubcommands)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(supportedSubcommands);

        if (args.Length == 1)
        {
            return CommandResult.InvalidArgument(
                command: commandName,
                message: $"Subcommand is required for command '{commandName}'. Supported subcommands: {string.Join(", ", supportedSubcommands)}.");
        }

        var secondArgument = args[1];
        if (CommandTokenClassifier.IsHelpOptionToken(secondArgument)
            || CommandTokenClassifier.IsVersionOptionToken(secondArgument))
        {
            return null;
        }

        for (var i = 0; i < supportedSubcommands.Count; i++)
        {
            if (string.Equals(secondArgument, supportedSubcommands[i], StringComparison.Ordinal))
            {
                return null;
            }
        }

        return CommandResult.InvalidArgument(
            command: commandName,
            message: $"Subcommand '{secondArgument}' is not recognized for command '{commandName}'.");
    }
}
