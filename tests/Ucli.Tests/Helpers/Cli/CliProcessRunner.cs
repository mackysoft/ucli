namespace MackySoft.Tests;

using System.Diagnostics;
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

        using var process = new Process();
        var startInfo = process.StartInfo;
        startInfo.FileName = "dotnet";
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardInput = standardInput is not null;
        startInfo.CreateNoWindow = true;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(key);
                    continue;
                }

                startInfo.Environment[key] = value;
            }
        }

        startInfo.ArgumentList.Add(toolPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var started = process.Start();
        Assert.True(started, "Failed to start ucli process.");

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(processTimeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException($"ucli process timed out after {processTimeout.TotalSeconds} seconds.");
        }

        return new CommandExecutionResult(
            ExitCode: process.ExitCode,
            StdOut: await stdOutTask,
            StdErr: await stdErrTask);
    }
}
