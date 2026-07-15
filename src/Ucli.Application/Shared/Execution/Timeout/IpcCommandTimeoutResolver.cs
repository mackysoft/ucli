using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.Timeout;

/// <summary> Resolves IPC command timeout from command options and config defaults. </summary>
internal static class IpcCommandTimeoutResolver
{
    /// <summary> Resolves the effective IPC timeout from an optional normalized command value and config defaults. </summary>
    /// <param name="optionValue"> The optional normalized command option value in milliseconds. </param>
    /// <param name="command"> The command used to apply per-command timeout overrides. </param>
    /// <param name="config"> The loaded config values. </param>
    /// <returns> The timeout-resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="command" /> or <paramref name="config" /> is <see langword="null" />. </exception>
    public static IpcCommandTimeoutResolutionResult ResolveNormalized (
        int? optionValue,
        UcliCommand command,
        UcliConfig config)
    {
        ArgumentNullException.ThrowIfNull(command);
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
