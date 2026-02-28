namespace MackySoft.Tests;

using System.Diagnostics;
using MackySoft.Ucli.Cli;

internal static class CliProcessRunner
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(15);

    public static Task<CommandExecutionResult> RunCommand (params string[] args)
    {
        return RunCommandCore(args, null);
    }

    public static Task<CommandExecutionResult> RunCommandWithWorkingDirectory (
        string workingDirectory,
        params string[] args)
    {
        return RunCommandCore(args, workingDirectory);
    }

    private static async Task<CommandExecutionResult> RunCommandCore (
        string[] args,
        string? workingDirectory)
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
        startInfo.CreateNoWindow = true;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        startInfo.ArgumentList.Add(toolPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var started = process.Start();
        Assert.True(started, "Failed to start ucli process.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(ProcessTimeout);
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

            throw new TimeoutException($"ucli process timed out after {ProcessTimeout.TotalSeconds} seconds.");
        }

        return new CommandExecutionResult(
            ExitCode: process.ExitCode,
            StdOut: await stdOutTask,
            StdErr: await stdErrTask);
    }
}