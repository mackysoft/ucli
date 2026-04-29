using System.Globalization;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Shared.Execution.Timeout;

/// <summary> Resolves IPC command timeout from command options and config defaults. </summary>
internal static class IpcCommandTimeoutResolver
{
    /// <summary> Resolves the effective IPC timeout from optional command value and config defaults. </summary>
    /// <param name="optionValue"> The optional command option value in milliseconds. </param>
    /// <param name="command"> The command used to apply per-command timeout overrides. </param>
    /// <param name="config"> The loaded config values. </param>
    /// <returns> The timeout-resolution result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="command" /> has an invalid name. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public static IpcCommandTimeoutResolutionResult Resolve (
        string? optionValue,
        UcliCommand command,
        UcliConfig config)
    {
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        ArgumentNullException.ThrowIfNull(config);

        if (optionValue is null)
        {
            if (config.IpcTimeoutMillisecondsByCommand.TryGetValue(command.Name, out var commandTimeoutMilliseconds)
                && commandTimeoutMilliseconds.HasValue)
            {
                return ResolveMilliseconds(
                    commandTimeoutMilliseconds.Value,
                    $"config ipcTimeoutMillisecondsByCommand[{command.Name}]");
            }

            return ResolveMilliseconds(config.IpcDefaultTimeoutMilliseconds, "config ipcDefaultTimeoutMilliseconds");
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(optionValue, out var normalizedOptionValue))
        {
            return IpcCommandTimeoutResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"timeout must be a positive integer milliseconds value. Actual: {optionValue}."));
        }

        if (!int.TryParse(normalizedOptionValue, NumberStyles.None, CultureInfo.InvariantCulture, out var timeoutMilliseconds))
        {
            return IpcCommandTimeoutResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"timeout must be a positive integer milliseconds value. Actual: {optionValue}."));
        }

        return ResolveMilliseconds(timeoutMilliseconds, "timeout");
    }

    /// <summary> Resolves the effective IPC timeout from an optional normalized command value and config defaults. </summary>
    /// <param name="optionValue"> The optional normalized command option value in milliseconds. </param>
    /// <param name="command"> The command used to apply per-command timeout overrides. </param>
    /// <param name="config"> The loaded config values. </param>
    /// <returns> The timeout-resolution result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="command" /> has an invalid name. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public static IpcCommandTimeoutResolutionResult ResolveNormalized (
        int? optionValue,
        UcliCommand command,
        UcliConfig config)
    {
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        ArgumentNullException.ThrowIfNull(config);

        if (!optionValue.HasValue)
        {
            if (config.IpcTimeoutMillisecondsByCommand.TryGetValue(command.Name, out var commandTimeoutMilliseconds)
                && commandTimeoutMilliseconds.HasValue)
            {
                return ResolveMilliseconds(
                    commandTimeoutMilliseconds.Value,
                    $"config ipcTimeoutMillisecondsByCommand[{command.Name}]");
            }

            return ResolveMilliseconds(config.IpcDefaultTimeoutMilliseconds, "config ipcDefaultTimeoutMilliseconds");
        }

        return ResolveMilliseconds(optionValue.Value, "timeout");
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
