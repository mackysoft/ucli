using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Validates and normalizes IPC-timeout config values. </summary>
internal static class IpcTimeoutConfigValidator
{
    /// <summary> Parses one timeout value in milliseconds. </summary>
    /// <param name="value"> The timeout value in milliseconds. </param>
    /// <param name="timeoutMilliseconds"> The parsed timeout value. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParseTimeoutMilliseconds (
        int value,
        out int timeoutMilliseconds)
    {
        if (value <= 0)
        {
            timeoutMilliseconds = default;
            return false;
        }

        timeoutMilliseconds = value;
        return true;
    }

    /// <summary> Creates a serializable copy of command-timeout overrides. </summary>
    /// <param name="source"> The source command-timeout override map. </param>
    /// <returns> The serializable dictionary copy. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="source" /> is <see langword="null" />. </exception>
    public static Dictionary<string, int?> CreateSerializableCommandTimeoutOverrides (
        IReadOnlyDictionary<string, int?> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new Dictionary<string, int?>(
            source,
            StringComparer.Ordinal);
    }

    /// <summary> Creates a deterministic command-key list for diagnostics and error messages. </summary>
    /// <returns> The sorted command-key list. </returns>
    public static string GetSupportedCommandNamesDescription ()
    {
        return string.Join(", ", IpcTimeoutDefaults.SupportedCommands
            .Select(static command => command.Name)
            .OrderBy(static commandName => commandName, StringComparer.Ordinal));
    }

    /// <summary> Tries to normalize one supported timeout command name. </summary>
    /// <param name="commandName"> The command-name literal from config. </param>
    /// <param name="normalizedCommandName"> The normalized command name when successful. </param>
    /// <returns> <see langword="true" /> when the command is supported; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeSupportedCommandName (
        string? commandName,
        out string normalizedCommandName)
    {
        if (TryParseSupportedTimeoutCommand(commandName, out var command))
        {
            normalizedCommandName = command.Name;
            return true;
        }

        normalizedCommandName = string.Empty;
        return false;
    }

    private static bool TryParseSupportedTimeoutCommand (
        string? commandName,
        out UcliCommand command)
    {
        if (!UcliCommand.TryCreate(commandName, out command))
        {
            return false;
        }

        return IpcTimeoutDefaults.IsSupported(command);
    }
}
