namespace MackySoft.Ucli.Cli;

/// <summary> Defines CLI command-name constants and lookup helpers. </summary>
internal static class UcliCommandNames
{
    /// <summary> Gets the command name used when no subcommand can be identified. </summary>
    public const string Root = "root";

    /// <summary> Gets the command name for help. </summary>
    public const string Help = "help";

    /// <summary> Gets the command name for init. </summary>
    public const string Init = "init";

    /// <summary> Gets the command name for status. </summary>
    public const string Status = "status";

    /// <summary> Gets the top-level command name for test. </summary>
    public const string Test = "test";

    /// <summary> Gets the command name for <c>test profile init</c> result payloads. </summary>
    public const string TestProfileInit = "test.profile.init";

    /// <summary> Gets the nested command name for profile. </summary>
    public const string Profile = "profile";

    /// <summary> Gets the nested command name for init. </summary>
    public const string InitSubcommand = "init";

    private static readonly HashSet<string> RegisteredCommandNames = new(StringComparer.Ordinal)
    {
        Init,
        Status,
        Test,
    };

    /// <summary> Determines whether the specified command name is registered in the CLI host. </summary>
    /// <param name="commandName"> The command name to check. </param>
    /// <returns> <see langword="true" /> when the command is registered; otherwise <see langword="false" />. </returns>
    public static bool IsRegistered (string? commandName)
    {
        return !string.IsNullOrWhiteSpace(commandName) && RegisteredCommandNames.Contains(commandName);
    }

    /// <summary> Resolves the command name emitted in parse-error responses. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns>
    /// <para> A command name compatible with the CLI result envelope. </para>
    /// <para> Returns <see cref="Root" /> when no known command can be identified. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public static string ResolveResultCommandName (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return Root;
        }

        var firstArgument = args[0];
        if (CommandTokenClassifier.IsRootCommandToken(firstArgument))
        {
            return Root;
        }

        if (string.Equals(firstArgument, Test, StringComparison.Ordinal))
        {
            if (args.Length >= 3
                && string.Equals(args[1], Profile, StringComparison.Ordinal)
                && string.Equals(args[2], InitSubcommand, StringComparison.Ordinal))
            {
                return TestProfileInit;
            }

            return Test;
        }

        return IsRegistered(firstArgument) ? firstArgument : Root;
    }
}