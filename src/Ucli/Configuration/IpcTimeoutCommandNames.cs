namespace MackySoft.Ucli.Configuration;

/// <summary> Defines config keys for per-command IPC timeout overrides. </summary>
internal static class IpcTimeoutCommandNames
{
    /// <summary> Gets the command key for <c>status</c>. </summary>
    public const string Status = "status";

    /// <summary> Gets the command key for <c>validate</c>. </summary>
    public const string Validate = "validate";

    /// <summary> Gets the command key for <c>plan</c>. </summary>
    public const string Plan = "plan";

    /// <summary> Gets the command key for <c>call</c>. </summary>
    public const string Call = "call";

    /// <summary> Gets the command key for <c>resolve</c>. </summary>
    public const string Resolve = "resolve";

    /// <summary> Gets the command key for <c>query</c>. </summary>
    public const string Query = "query";

    /// <summary> Gets the command key for <c>refresh</c>. </summary>
    public const string Refresh = "refresh";

    /// <summary> Gets the command key for <c>ops</c>. </summary>
    public const string Ops = "ops";

    /// <summary> Gets the command key for <c>daemon</c>. </summary>
    public const string Daemon = "daemon";

    private static readonly HashSet<string> SupportedCommandNames = new(StringComparer.Ordinal)
    {
        Status,
        Validate,
        Plan,
        Call,
        Resolve,
        Query,
        Refresh,
        Ops,
        Daemon,
    };

    /// <summary> Gets the supported command-name keys used by config validation. </summary>
    public static IReadOnlyCollection<string> SupportedCommands => SupportedCommandNames;

    /// <summary> Creates default timeout overrides with all supported command keys set to <see langword="null" />. </summary>
    /// <returns> A mutable dictionary initialized with supported command keys. </returns>
    public static Dictionary<string, int?> CreateDefaultTimeoutOverrides ()
    {
        var result = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (var commandName in SupportedCommandNames)
        {
            result[commandName] = null;
        }

        return result;
    }

    /// <summary> Returns whether the specified command key is supported by timeout configuration. </summary>
    /// <param name="commandName"> The command key from config. </param>
    /// <returns> <see langword="true" /> when supported; otherwise <see langword="false" />. </returns>
    public static bool IsSupported (string? commandName)
    {
        return !string.IsNullOrWhiteSpace(commandName)
            && SupportedCommandNames.Contains(commandName);
    }
}