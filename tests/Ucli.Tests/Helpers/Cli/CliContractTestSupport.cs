namespace MackySoft.Tests;

using System.Diagnostics;
using System.Text.Json;
using MackySoft.Ucli.Cli;

internal static class CliContractAssertions
{
    public static void AssertCommandResultCommon (
        JsonElement root,
        string command,
        string status,
        int exitCode)
    {
        JsonAssert.For(root)
            .HasInt32("protocolVersion", CliProtocol.CurrentVersion)
            .HasString("command", command)
            .HasString("status", status)
            .HasInt32("exitCode", exitCode)
            .HasValueKind("message", JsonValueKind.String)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasValueKind("errors", JsonValueKind.Array);
    }

    public static void AssertNoErrors (JsonElement root)
    {
        JsonAssert.For(root)
            .HasArrayLength("errors", 0);
    }

    public static void AssertSingleError (JsonElement root, string expectedCode)
    {
        JsonAssert.For(root)
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", expectedCode)
                .HasValueKind("message", JsonValueKind.String)
                .IsNull("opId"));
    }
}

internal static class CliToolRunner
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(15);

    public static Task<CommandExecutionResult> RunAsync (params string[] args)
    {
        return RunCoreAsync(args, null);
    }

    public static Task<CommandExecutionResult> RunWithWorkingDirectoryAsync (
        string workingDirectory,
        params string[] args)
    {
        return RunCoreAsync(args, workingDirectory);
    }

    private static async Task<CommandExecutionResult> RunCoreAsync (
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

internal readonly record struct CommandExecutionResult (
    int ExitCode,
    string StdOut,
    string StdErr);
