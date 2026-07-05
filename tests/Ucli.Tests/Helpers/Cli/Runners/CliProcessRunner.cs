namespace MackySoft.Tests;

using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

internal static class CliProcessRunner
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(15);

    public static Task<CommandExecutionResult> RunCommandAsync (params string[] args)
    {
        return RunCommandCoreAsync(args, null, null, ProcessTimeout);
    }

    public static Task<CommandExecutionResult> RunCommandWithTimeoutAsync (
        TimeSpan processTimeout,
        params string[] args)
    {
        return RunCommandCoreAsync(args, null, null, processTimeout);
    }

    public static Task<CommandExecutionResult> RunCommandWithEnvironmentAsync (
        IReadOnlyDictionary<string, string?> environmentVariables,
        params string[] args)
    {
        return RunCommandCoreAsync(args, null, null, ProcessTimeout, environmentVariables);
    }

    public static Task<CommandExecutionResult> RunCommandWithStandardInputAsync (
        string standardInput,
        params string[] args)
    {
        return RunCommandCoreAsync(args, null, standardInput, ProcessTimeout);
    }

    public static Task<CommandExecutionResult> RunCommandWithWorkingDirectoryAsync (
        string workingDirectory,
        params string[] args)
    {
        return RunCommandCoreAsync(args, workingDirectory, null, ProcessTimeout);
    }

    public static Task<CommandExecutionResult> RunCommandWithWorkingDirectoryAndStandardInputAsync (
        string workingDirectory,
        string standardInput,
        params string[] args)
    {
        return RunCommandCoreAsync(args, workingDirectory, standardInput, ProcessTimeout);
    }

    private static async Task<CommandExecutionResult> RunCommandCoreAsync (
        string[] args,
        string? workingDirectory,
        string? standardInput,
        TimeSpan processTimeout,
        IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        // NOTE:
        // This helper executes the built CLI process to validate stdout/stderr and exit-code
        // contracts from Program, including parser and global exception pathways.
        var toolPath = typeof(CommandResult).Assembly.Location;
        Assert.True(File.Exists(toolPath), $"CLI assembly was not found: {toolPath}");

        TestProcessResult result = await TestProcessRunner.RunAsync(
                "dotnet",
                [toolPath, .. args],
                string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
                environmentVariables,
                standardInput,
                processTimeout)
            .ConfigureAwait(false);
        return new CommandExecutionResult(
            ExitCode: result.ExitCode,
            StdOut: result.StdOut,
            StdErr: result.StdErr);
    }
}
