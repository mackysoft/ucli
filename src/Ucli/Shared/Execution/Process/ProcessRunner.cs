using System.Diagnostics;
using System.Text;
using MackySoft.Ucli.Contracts.Text;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Implements process execution with timeout, cancellation, and bounded output capture. </summary>
internal sealed class ProcessRunner : IProcessRunner
{
    private const int MaxCapturedOutputChars = 4096;

    /// <summary> Runs one process request. </summary>
    /// <param name="request"> The process request values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by the caller. </param>
    /// <returns> A task that resolves to the process execution result. </returns>
    public async Task<ProcessRunResult> RunAsync (
        ProcessRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var process = new DiagnosticsProcess();
        var startInfo = process.StartInfo;
        startInfo.FileName = request.FileName;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var standardOutput = new StringBuilder();
        var fullStandardOutput = request.CaptureStandardOutput ? new StringBuilder() : null;
        var standardError = new StringBuilder();
        var standardOutputCompleted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var standardErrorCompleted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, eventArgs) => HandleOutputData(standardOutput, fullStandardOutput, standardOutputCompleted, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => HandleOutputData(standardError, standardErrorCompleted, eventArgs.Data);

        try
        {
            if (!process.Start())
            {
                return ProcessRunResult.StartFailed("Failed to start process.");
            }
        }
        catch (Exception exception)
        {
            return ProcessRunResult.StartFailed($"Failed to start process: {exception.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCancellationTokenSource = new CancellationTokenSource(request.Timeout);
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellationTokenSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryKillProcessAsync(process).ConfigureAwait(false);
            await DrainOutputAsync(standardOutputCompleted.Task, standardErrorCompleted.Task).ConfigureAwait(false);
            return ProcessRunResult.Canceled(
                $"Process execution was canceled.{BuildOutputSnippet(standardError, standardOutput)}",
                standardOutput: standardOutput.Length > 0 ? standardOutput.ToString() : null);
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested
                                                 && !cancellationToken.IsCancellationRequested)
        {
            await TryKillProcessAsync(process).ConfigureAwait(false);
            await DrainOutputAsync(standardOutputCompleted.Task, standardErrorCompleted.Task).ConfigureAwait(false);
            return ProcessRunResult.TimedOut(
                $"Process timed out after {request.Timeout.TotalMilliseconds:0} milliseconds.{BuildOutputSnippet(standardError, standardOutput)}",
                standardOutput: standardOutput.Length > 0 ? standardOutput.ToString() : null);
        }

        await DrainOutputAsync(standardOutputCompleted.Task, standardErrorCompleted.Task).ConfigureAwait(false);

        if (process.ExitCode == 0)
        {
            return ProcessRunResult.Exited(
                0,
                standardOutput: GetCapturedStandardOutput(standardOutput, fullStandardOutput));
        }

        return ProcessRunResult.Exited(
            process.ExitCode,
            $"Process exited with code {process.ExitCode}.{BuildOutputSnippet(standardError, standardOutput)}",
            standardOutput: GetCapturedStandardOutput(standardOutput, fullStandardOutput));
    }

    /// <summary> Tries to terminate one running process and waits for process exit. </summary>
    /// <param name="process"> The process to terminate. </param>
    private static async Task TryKillProcessAsync (DiagnosticsProcess process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
                // NOTE: Process may already be terminated by the runtime.
            }
        }

        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // NOTE: Ignore wait failures after kill attempts because failure classification is already decided.
        }
    }

    /// <summary> Waits for redirected output stream completion. </summary>
    /// <param name="standardOutputCompleted"> The standard-output completion task. </param>
    /// <param name="standardErrorCompleted"> The standard-error completion task. </param>
    private static async Task DrainOutputAsync (
        Task standardOutputCompleted,
        Task standardErrorCompleted)
    {
        await Task.WhenAll(standardOutputCompleted, standardErrorCompleted).ConfigureAwait(false);
    }

    /// <summary> Handles one redirected output line. </summary>
    /// <param name="buffer"> The target output buffer. </param>
    /// <param name="completionSource"> The stream completion source. </param>
    /// <param name="line"> The emitted output line. </param>
    private static void HandleOutputData (
        StringBuilder buffer,
        StringBuilder? fullBuffer,
        TaskCompletionSource<object?> completionSource,
        string? line)
    {
        if (line is null)
        {
            completionSource.TrySetResult(null);
            return;
        }

        AppendOutput(buffer, line);
        AppendFullOutput(fullBuffer, line);
    }

    private static void HandleOutputData (
        StringBuilder buffer,
        TaskCompletionSource<object?> completionSource,
        string? line)
    {
        if (line is null)
        {
            completionSource.TrySetResult(null);
            return;
        }

        AppendOutput(buffer, line);
    }

    /// <summary> Appends one output line into bounded output buffer. </summary>
    /// <param name="buffer"> The output buffer. </param>
    /// <param name="line"> The output line. </param>
    private static void AppendOutput (
        StringBuilder buffer,
        string line)
    {
        if (line.Length == 0 || buffer.Length >= MaxCapturedOutputChars)
        {
            return;
        }

        var remainingChars = MaxCapturedOutputChars - buffer.Length;
        if (line.Length + Environment.NewLine.Length <= remainingChars)
        {
            buffer.AppendLine(line);
            return;
        }

        buffer.Append(line.AsSpan(0, Math.Min(line.Length, remainingChars)));
    }

    private static void AppendFullOutput (
        StringBuilder? buffer,
        string line)
    {
        if (buffer == null)
        {
            return;
        }

        buffer.AppendLine(line);
    }

    /// <summary> Gets the captured standard-output text according to the request mode. </summary>
    /// <param name="boundedBuffer"> The bounded diagnostic output buffer. </param>
    /// <param name="fullBuffer"> The full output buffer when capture is enabled. </param>
    /// <returns> The captured standard-output text when available; otherwise <see langword="null" />. </returns>
    private static string? GetCapturedStandardOutput (
        StringBuilder boundedBuffer,
        StringBuilder? fullBuffer)
    {
        if (fullBuffer != null)
        {
            return fullBuffer.Length > 0 ? fullBuffer.ToString() : null;
        }

        return null;
    }

    /// <summary> Builds one concise output snippet from captured output buffers. </summary>
    /// <param name="standardError"> The captured standard-error output. </param>
    /// <param name="standardOutput"> The captured standard-output output. </param>
    /// <returns> The output snippet text. </returns>
    private static string BuildOutputSnippet (
        StringBuilder standardError,
        StringBuilder standardOutput)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(
                standardError.Length > 0 ? standardError.ToString() : standardOutput.ToString(),
                out var output))
        {
            return string.Empty;
        }

        return $" Output: {output}";
    }
}
