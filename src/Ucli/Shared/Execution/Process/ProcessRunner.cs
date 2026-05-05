using System.Text;
using MackySoft.Ucli.Contracts.Text;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Implements process execution with timeout, cancellation, and bounded output capture. </summary>
internal sealed class ProcessRunner : IProcessRunner
{
    private const int MaxCapturedOutputChars = 4096;

    private static readonly TimeSpan ProcessKillWaitTimeout = TimeSpan.FromSeconds(5);

    /// <summary> Runs one process request. </summary>
    /// <param name="request"> The process request values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by the caller. </param>
    /// <returns> A task that resolves to the process execution result. </returns>
    public async Task<ProcessRunResult> RunAsync (
        ProcessRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOutputDrainMode(request.OutputDrainMode);

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
            await WaitForProcessExitOnlyAsync(process, linkedCancellationTokenSource.Token).ConfigureAwait(false);
            await DrainOutputAsync(
                standardOutputCompleted.Task,
                standardErrorCompleted.Task,
                request.OutputDrainMode,
                linkedCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryKillProcessAsync(process).ConfigureAwait(false);
            await TryDrainOutputBestEffortAsync(standardOutputCompleted.Task, standardErrorCompleted.Task).ConfigureAwait(false);
            return ProcessRunResult.Canceled(
                $"Process execution was canceled.{BuildOutputSnippet(standardError, standardOutput)}",
                standardOutput: standardOutput.Length > 0 ? standardOutput.ToString() : null);
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested
                                                 && !cancellationToken.IsCancellationRequested)
        {
            await TryKillProcessAsync(process).ConfigureAwait(false);
            await TryDrainOutputBestEffortAsync(standardOutputCompleted.Task, standardErrorCompleted.Task).ConfigureAwait(false);
            return ProcessRunResult.TimedOut(
                $"Process timed out after {request.Timeout.TotalMilliseconds:0} milliseconds.{BuildOutputSnippet(standardError, standardOutput)}",
                standardOutput: standardOutput.Length > 0 ? standardOutput.ToString() : null);
        }

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

    /// <summary> Validates one output drain mode value before process execution starts. </summary>
    /// <param name="outputDrainMode"> The output drain mode to validate. </param>
    private static void ValidateOutputDrainMode (ProcessOutputDrainMode outputDrainMode)
    {
        if (outputDrainMode is ProcessOutputDrainMode.WaitForCompletion or ProcessOutputDrainMode.BestEffort)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(outputDrainMode), outputDrainMode, "Unknown process output drain mode.");
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
            using var killWaitCancellationTokenSource = new CancellationTokenSource(ProcessKillWaitTimeout);
            await WaitForProcessExitOnlyAsync(process, killWaitCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // NOTE: Ignore wait failures after kill attempts because failure classification is already decided.
        }
    }

    /// <summary> Waits only for the process exit signal without waiting for redirected stream completion. </summary>
    /// <param name="process"> The process to observe. </param>
    /// <param name="cancellationToken"> A cancellation token for the wait operation. </param>
    private static async Task WaitForProcessExitOnlyAsync (
        DiagnosticsProcess process,
        CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return;
        }

        var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnExited (object? _, EventArgs __)
        {
            completionSource.TrySetResult(null);
        }

        process.EnableRaisingEvents = true;
        process.Exited += OnExited;

        try
        {
            if (process.HasExited)
            {
                return;
            }

            using var cancellationRegistration = cancellationToken.Register(
                static state => ((TaskCompletionSource<object?>)state!).TrySetCanceled(),
                completionSource);
            await completionSource.Task.ConfigureAwait(false);
        }
        finally
        {
            process.Exited -= OnExited;
        }
    }

    /// <summary> Drains redirected output streams according to the requested contract. </summary>
    /// <param name="standardOutputCompleted"> The standard-output completion task. </param>
    /// <param name="standardErrorCompleted"> The standard-error completion task. </param>
    /// <param name="outputDrainMode"> The output drain mode requested by the caller. </param>
    /// <param name="cancellationToken"> A cancellation token for required output completion. </param>
    private static async Task DrainOutputAsync (
        Task standardOutputCompleted,
        Task standardErrorCompleted,
        ProcessOutputDrainMode outputDrainMode,
        CancellationToken cancellationToken)
    {
        switch (outputDrainMode)
        {
            case ProcessOutputDrainMode.WaitForCompletion:
                await Task.WhenAll(standardOutputCompleted, standardErrorCompleted)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                return;

            case ProcessOutputDrainMode.BestEffort:
                await TryDrainOutputBestEffortAsync(standardOutputCompleted, standardErrorCompleted).ConfigureAwait(false);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(outputDrainMode), outputDrainMode, "Unknown process output drain mode.");
        }
    }

    /// <summary> Observes redirected output stream completion only when it is already available. </summary>
    /// <param name="standardOutputCompleted"> The standard-output completion task. </param>
    /// <param name="standardErrorCompleted"> The standard-error completion task. </param>
    private static async Task TryDrainOutputBestEffortAsync (
        Task standardOutputCompleted,
        Task standardErrorCompleted)
    {
        var drainTask = Task.WhenAll(standardOutputCompleted, standardErrorCompleted);
        if (!drainTask.IsCompleted)
        {
            return;
        }

        await drainTask.ConfigureAwait(false);
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
