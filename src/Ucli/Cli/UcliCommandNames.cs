namespace MackySoft.Ucli.Cli;

/// <summary> Defines CLI command-name constants and lookup helpers. </summary>
internal static class UcliCommandNames
{
    /// <summary> Gets the command name for help. </summary>
    public const string Help = "help";

    /// <summary> Gets the command name for init. </summary>
    public const string Init = "init";

    /// <summary> Gets the command name for status. </summary>
    public const string Status = "status";

    private static readonly HashSet<string> RegisteredCommandNames = new(StringComparer.Ordinal)
    {
        Init,
        Status,
    };

    /// <summary> Determines whether the specified command name is registered in the CLI host. </summary>
    /// <param name="commandName"> The command name to check. </param>
    /// <returns> <see langword="true" /> when the command is registered; otherwise <see langword="false" />. </returns>
    public static bool IsRegistered (string? commandName)
    {
        return !string.IsNullOrWhiteSpace(commandName) && RegisteredCommandNames.Contains(commandName);
    }
}