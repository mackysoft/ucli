using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;

namespace MackySoft.Ucli.Hosting.Cli.Common.Parsing;

/// <summary> Creates JSON command errors for arguments that must be rejected before framework dispatch. </summary>
internal static class CliPreDispatchErrorPolicy
{
    /// <summary> Creates a pre-dispatch error result for unknown commands or unsupported subcommands. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns> The error result to emit, or <see langword="null" /> when framework dispatch should continue. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public static CommandResult? TryCreateErrorResult (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return null;
        }

        var firstArgument = args[0];
        if (CommandTokenClassifier.IsRootCommandToken(firstArgument))
        {
            return null;
        }

        if (UcliCommandCatalog.TryGetSupportedSubcommands(firstArgument, out var supportedSubcommands))
        {
            var invalidSubcommandResult = SubcommandValidationHelper.TryCreateInvalidSubcommandResult(
                args,
                firstArgument,
                supportedSubcommands);
            if (invalidSubcommandResult != null)
            {
                return invalidSubcommandResult;
            }

            var unexpectedLeafArgumentResult = TryCreateUnexpectedLeafArgumentResult(args, firstArgument);
            if (unexpectedLeafArgumentResult != null)
            {
                return unexpectedLeafArgumentResult;
            }

            return TryCreateInvalidLeafSubcommandResult(args, firstArgument);
        }

        if (UcliCommandCatalog.IsRegisteredRootCommand(firstArgument)
            || string.Equals(firstArgument, UcliCommandNames.Help, StringComparison.Ordinal))
        {
            return null;
        }

        return CommandResult.InvalidArgument(
            command: UcliCommandNames.Root,
            message: $"Command '{firstArgument}' is not recognized.");
    }

    private static CommandResult? TryCreateUnexpectedLeafArgumentResult (
        string[] args,
        string commandName)
    {
        if (args.Length < 2
            || !UcliCommandCatalog.TryGetUnexpectedLeafArgumentRule(commandName, args[1], out var rule))
        {
            return null;
        }

        return SubcommandValidationHelper.TryCreateUnexpectedLeafArgumentResult(
            args,
            rule.CommandName,
            rule.SubcommandName,
            rule.ResultCommandName,
            rule.ExpectedArgumentCount);
    }

    private static CommandResult? TryCreateInvalidLeafSubcommandResult (
        string[] args,
        string commandName)
    {
        if (args.Length < 2
            || !UcliCommandCatalog.TryGetSupportedLeafSubcommands(commandName, args[1], out var supportedSubcommands))
        {
            return null;
        }

        var group = args[1];
        if (args.Length == 2)
        {
            return CommandResult.InvalidArgument(
                command: commandName,
                message: $"Subcommand is required for command '{commandName} {group}'. Supported subcommands: {string.Join(", ", supportedSubcommands)}.");
        }

        var subcommand = args[2];
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
            command: commandName,
            message: $"Subcommand '{subcommand}' is not recognized for command '{commandName} {group}'.");
    }
}
