namespace MackySoft.Ucli.Configuration;

/// <summary> Defines config keys for per-command IPC timeout overrides. </summary>
internal static class IpcTimeoutCommandNames
{
    /// <summary> Gets supported command-name keys used by config validation. </summary>
    public static IReadOnlyCollection<string> SupportedCommandNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "status",
        "validate",
        "plan",
        "call",
        "resolve",
        "query",
        "refresh",
        "ops",
        "daemon",
    };

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