namespace MackySoft.Tests;

internal static class CommandResultCapture
{
    public static async Task<CommandExecutionResult> ExecuteAsync (Func<Task<int>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        var result = await StandardOutputCapture.ExecuteAsync(executeAsync).ConfigureAwait(false);
        return new CommandExecutionResult(result.ExitCode, result.StandardOutput, string.Empty);
    }

    public static Task<CommandExecutionResult> ExecuteSynchronousCommandAsync (Func<int> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);

        return ExecuteAsync(() => Task.FromResult(execute()));
    }

    public static async Task<CommandExecutionResult> ExecuteWithErrorAsync (Func<Task<int>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(executeAsync)
            .ConfigureAwait(false);
        return new CommandExecutionResult(exitCode, standardOutput, standardError);
    }
}
