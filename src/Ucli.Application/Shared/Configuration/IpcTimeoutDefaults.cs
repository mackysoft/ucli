namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Defines command keys and defaults for per-command IPC timeout overrides. </summary>
internal static class IpcTimeoutDefaults
{
    /// <summary> Gets global fallback IPC timeout in milliseconds. </summary>
    public const int GlobalTimeoutMilliseconds = 3000;

    private static readonly (UcliCommand Command, int DefaultTimeoutMilliseconds)[] DefaultTimeoutEntries =
    [
        (UcliCommandIds.Test, 300000),
        (UcliCommandIds.Status, 5000),
        (UcliCommandIds.Validate, 10000),
        (UcliCommandIds.Plan, 20000),
        (UcliCommandIds.Call, 60000),
        (UcliCommandIds.Resolve, 10000),
        (UcliCommandIds.Query, 10000),
        (UcliCommandIds.Refresh, 120000),
        (UcliCommandIds.Ops, 120000),
        (UcliCommandIds.DaemonStart, 60000),
        (UcliCommandIds.DaemonStop, 10000),
        (UcliCommandIds.DaemonCleanup, 3000),
        (UcliCommandIds.DaemonStatus, 3000),
        (UcliCommandIds.DaemonList, 3000),
        (UcliCommandIds.LogsDaemon, 3000),
        (UcliCommandIds.LogsUnity, 3000),
    ];

    private static readonly HashSet<UcliCommand> SupportedCommandSet = CreateSupportedCommandSet();

    /// <summary> Gets supported commands used by timeout-config validation. </summary>
    public static IReadOnlyCollection<UcliCommand> SupportedCommands { get; } = SupportedCommandSet;

    /// <summary> Creates default timeout overrides with all supported command keys set to configured default values. </summary>
    /// <returns> A mutable dictionary initialized with supported command keys. </returns>
    public static Dictionary<string, int?> CreateDefaultTimeoutOverrides ()
    {
        var result = new Dictionary<string, int?>(DefaultTimeoutEntries.Length, StringComparer.Ordinal);

        foreach (var timeoutEntry in DefaultTimeoutEntries)
        {
            result[timeoutEntry.Command.Name] = timeoutEntry.DefaultTimeoutMilliseconds;
        }

        return result;
    }

    /// <summary> Returns whether the specified command is supported by timeout configuration. </summary>
    /// <param name="command"> The command to check. </param>
    /// <returns> <see langword="true" /> when supported; otherwise <see langword="false" />. </returns>
    public static bool IsSupported (UcliCommand command)
    {
        return SupportedCommandSet.Contains(command);
    }

    private static HashSet<UcliCommand> CreateSupportedCommandSet ()
    {
        var result = new HashSet<UcliCommand>();
        foreach (var timeoutEntry in DefaultTimeoutEntries)
        {
            result.Add(timeoutEntry.Command);
        }

        return result;
    }
}
