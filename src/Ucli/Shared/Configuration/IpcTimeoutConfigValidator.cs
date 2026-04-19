using System.Linq;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Shared.Configuration;

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

    /// <summary> Parses command-timeout override values from config JSON data. </summary>
    /// <param name="source">
    /// <para> The raw command-timeout override map from config JSON. </para>
    /// <para> When <see langword="null" />, default command entries are created with <see langword="null" /> timeout values. </para>
    /// </param>
    /// <param name="timeoutsByCommand"> The normalized timeout map keyed by command name. </param>
    /// <param name="error"> The parse error when parse fails. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParseCommandTimeoutOverrides (
        IReadOnlyDictionary<string, int?>? source,
        out Dictionary<string, int?> timeoutsByCommand,
        out ExecutionError? error)
    {
        timeoutsByCommand = source is null
            ? IpcTimeoutDefaults.CreateDefaultTimeoutOverrides()
            : new Dictionary<string, int?>(StringComparer.Ordinal);

        if (source is null)
        {
            error = null;
            return true;
        }

        foreach (var entry in source)
        {
            if (!TryParseSupportedTimeoutCommand(entry.Key, out var command))
            {
                var supportedCommands = GetSupportedCommandNamesDescription();
                error = ExecutionError.InvalidArgument(
                    $"Config ipcTimeoutMillisecondsByCommand contains unsupported command key: {entry.Key}. Supported: {supportedCommands}.");
                return false;
            }

            if (entry.Value.HasValue
                && !TryParseTimeoutMilliseconds(entry.Value.Value, out _))
            {
                error = ExecutionError.InvalidArgument(
                    $"Config ipcTimeoutMillisecondsByCommand[{entry.Key}] is invalid: {entry.Value.Value}.");
                return false;
            }

            timeoutsByCommand[command.Name] = entry.Value;
        }

        error = null;
        return true;
    }

    /// <summary> Validates command-timeout override values before saving config. </summary>
    /// <param name="source">
    /// <para> The command-timeout override map in config values. </para>
    /// <para> Must not be <see langword="null" />. </para>
    /// </param>
    /// <param name="error"> The validation error when validation fails. </param>
    /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryValidateCommandTimeoutOverrides (
        IReadOnlyDictionary<string, int?>? source,
        out ExecutionError? error)
    {
        if (source is null)
        {
            error = ExecutionError.InvalidArgument("Config ipcTimeoutMillisecondsByCommand must not be null.");
            return false;
        }

        foreach (var entry in source)
        {
            if (!TryParseSupportedTimeoutCommand(entry.Key, out _))
            {
                var supportedCommands = GetSupportedCommandNamesDescription();
                error = ExecutionError.InvalidArgument(
                    $"Config ipcTimeoutMillisecondsByCommand contains unsupported command key: {entry.Key}. Supported: {supportedCommands}.");
                return false;
            }

            if (entry.Value.HasValue
                && !TryParseTimeoutMilliseconds(entry.Value.Value, out _))
            {
                error = ExecutionError.InvalidArgument(
                    $"Config ipcTimeoutMillisecondsByCommand[{entry.Key}] must be a positive integer or null. Actual: {entry.Value.Value}.");
                return false;
            }
        }

        error = null;
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

    /// <summary> Creates a deterministic command-key list for error messages. </summary>
    /// <returns> The sorted command-key list. </returns>
    private static string GetSupportedCommandNamesDescription ()
    {
        return string.Join(", ", IpcTimeoutDefaults.SupportedCommands
            .Select(static command => command.Name)
            .OrderBy(static commandName => commandName, StringComparer.Ordinal));
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