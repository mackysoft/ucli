using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;
using MackySoft.Ucli.Hosting.Cli.Common.Tokens;

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

        if (UcliCommandCatalog.TryGetPreDispatchSupportedSubcommands(firstArgument, out var supportedSubcommands))
        {
            var invalidSubcommandResult = SubcommandValidationHelper.TryCreateInvalidSubcommandResult(
                args,
                firstArgument,
                firstArgument,
                subcommandArgumentIndex: 1,
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

            return TryCreateInvalidNestedLeafSubcommandResult(args, firstArgument);
        }

        if (UcliCommandCatalog.IsRegisteredRootCommand(firstArgument))
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

    private static CommandResult? TryCreateInvalidNestedLeafSubcommandResult (
        string[] args,
        string commandName)
    {
        if (args.Length < 2)
        {
            return null;
        }

        if (!UcliCommandCatalog.TryGetSupportedLeafSubcommands(commandName, args[1], out var supportedSubcommands))
        {
            return null;
        }

        var group = args[1];
        return SubcommandValidationHelper.TryCreateInvalidSubcommandResult(
            args,
            commandName,
            $"{commandName} {group}",
            subcommandArgumentIndex: 2,
            supportedSubcommands);
    }
}
