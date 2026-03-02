using System.Globalization;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Execution;

/// <summary> Resolves IPC command timeout from command options and config defaults. </summary>
internal static class IpcCommandTimeoutResolver
{
    /// <summary> Resolves the effective IPC timeout from optional command value and config defaults. </summary>
    /// <param name="optionValue"> The optional command option value in milliseconds. </param>
    /// <param name="commandName"> The command name used to apply per-command timeout overrides. </param>
    /// <param name="config"> The loaded config values. </param>
    /// <returns> The timeout-resolution result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="commandName" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public static IpcCommandTimeoutResolutionResult Resolve (
        string? optionValue,
        string commandName,
        UcliConfig config)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            throw new ArgumentException("Command name must not be null or whitespace.", nameof(commandName));
        }

        ArgumentNullException.ThrowIfNull(config);

        if (optionValue is null)
        {
            if (config.IpcTimeoutMillisecondsByCommand.TryGetValue(commandName, out var commandTimeoutMilliseconds)
                && commandTimeoutMilliseconds.HasValue)
            {
                return ResolveMilliseconds(
                    commandTimeoutMilliseconds.Value,
                    $"config ipcTimeoutMillisecondsByCommand[{commandName}]");
            }

            return ResolveMilliseconds(config.IpcDefaultTimeoutMilliseconds, "config ipcDefaultTimeoutMilliseconds");
        }

        if (string.IsNullOrWhiteSpace(optionValue))
        {
            return IpcCommandTimeoutResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"timeout must be a positive integer milliseconds value. Actual: {optionValue}."));
        }

        var normalizedOptionValue = StringValueNormalizer.TrimToNull(optionValue)!;
        if (!int.TryParse(normalizedOptionValue, NumberStyles.None, CultureInfo.InvariantCulture, out var timeoutMilliseconds))
        {
            return IpcCommandTimeoutResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"timeout must be a positive integer milliseconds value. Actual: {optionValue}."));
        }

        return ResolveMilliseconds(timeoutMilliseconds, "timeout");
    }

    /// <summary> Converts millisecond input to timeout value with validation. </summary>
    /// <param name="timeoutMilliseconds"> The timeout value in milliseconds. </param>
    /// <param name="sourceLabel"> The value source label used in error messages. </param>
    /// <returns> The timeout-resolution result. </returns>
    private static IpcCommandTimeoutResolutionResult ResolveMilliseconds (
        int timeoutMilliseconds,
        string sourceLabel)
    {
        if (timeoutMilliseconds <= 0)
        {
            return IpcCommandTimeoutResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"{sourceLabel} must be a positive integer milliseconds value. Actual: {timeoutMilliseconds}."));
        }

        return IpcCommandTimeoutResolutionResult.Success(TimeSpan.FromMilliseconds(timeoutMilliseconds));
    }
}