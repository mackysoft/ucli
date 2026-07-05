namespace MackySoft.Tests;

using System.Diagnostics;
using System.Text;

internal static class TestProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static readonly Encoding StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static async Task<TestProcessResult> RunRequiredAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? standardInput = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        TestProcessResult result = await RunAsync(
            fileName,
            arguments,
            workingDirectory,
            environment,
            standardInput,
            timeout,
            cancellationToken);
        Assert.True(
            result.ExitCode == 0,
            $"{fileName} {string.Join(" ", arguments)} failed in {workingDirectory} with exit code {result.ExitCode}." +
            $"{Environment.NewLine}StdOut:{Environment.NewLine}{result.StdOut}" +
            $"{Environment.NewLine}StdErr:{Environment.NewLine}{result.StdErr}");
        return result;
    }

    public static async Task<TestProcessResult> RunAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? standardInput = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process();
        ProcessStartInfo startInfo = process.StartInfo;
        startInfo.FileName = fileName;
        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardInput = standardInput is not null;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        if (standardInput is not null)
        {
            startInfo.StandardInputEncoding = StandardInputEncoding;
        }

        startInfo.CreateNoWindow = true;
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach ((string name, string? value) in environment)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(name);
                    continue;
                }

                startInfo.Environment[name] = value;
            }
        }

        bool started = process.Start();
        Assert.True(started, $"Failed to start process: {fileName}");

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        if (standardInput is not null)
        {
            await WriteStandardInputAsync(process, standardInput, cancellationToken);
        }

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? DefaultTimeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TestProcessAwaiter.TerminateBestEffort(process);
            throw new TimeoutException($"{fileName} did not exit within {(timeout ?? DefaultTimeout)}.");
        }

        return new TestProcessResult(
            process.ExitCode,
            await stdOutTask,
            await stdErrTask);
    }

    private static async Task WriteStandardInputAsync (
        Process process,
        string standardInput,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        catch (IOException)
        {
            // NOTE: Some negative-path process tests intentionally let the child exit before reading stdin.
        }
        catch (ObjectDisposedException)
        {
            // NOTE: See the IOException path above.
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

}
