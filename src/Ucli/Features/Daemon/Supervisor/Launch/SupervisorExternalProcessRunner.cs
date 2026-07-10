using System.ComponentModel;
using System.Diagnostics;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Executes external helper processes used during supervisor bootstrap. </summary>
internal sealed class SupervisorExternalProcessRunner
{
    /// <summary> Executes one external process and captures its standard streams. </summary>
    /// <param name="fileName"> The executable file name. </param>
    /// <param name="arguments"> The argument list passed to the process. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The captured process execution result. </returns>
    public async ValueTask<SupervisorExternalProcessExecutionResult> RunAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        for (var i = 0; i < arguments.Count; i++)
        {
            startInfo.ArgumentList.Add(arguments[i]);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Process could not be started: {fileName}");
        try
        {
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);
            return new SupervisorExternalProcessExecutionResult(process.ExitCode, standardOutput, standardError);
        }
        catch (Exception)
        {
            await TerminateBestEffortAsync(process).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary> Executes one external process and suppresses any bootstrap-recovery failure. </summary>
    /// <param name="fileName"> The executable file name. </param>
    /// <param name="arguments"> The argument list passed to the process. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    public async ValueTask RunIgnoringExitCodeAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await RunAsync(fileName, arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            // NOTE:
            // bootstrap recovery intentionally ignores cleanup failures because the service
            // may simply be absent when the supervisor is launched for the first time, and
            // the subsequent required bootstrap step will report a structured failure.
        }
    }

    /// <summary> Formats one process execution result for structured internal-error messages. </summary>
    /// <param name="result"> The captured process execution result. </param>
    /// <returns> The formatted failure message. </returns>
    public static string FormatFailure (SupervisorExternalProcessExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var output = result.StandardOutput.Trim();
        var error = result.StandardError.Trim();
        if (output.Length == 0 && error.Length == 0)
        {
            return $"ExitCode={result.ExitCode}.";
        }

        if (output.Length == 0)
        {
            return $"ExitCode={result.ExitCode}. stderr={error}";
        }

        if (error.Length == 0)
        {
            return $"ExitCode={result.ExitCode}. stdout={output}";
        }

        return $"ExitCode={result.ExitCode}. stdout={output} stderr={error}";
    }

    private static async ValueTask TerminateBestEffortAsync (Process process)
    {
        try
        {
            _ = await ProcessTerminator.TerminateAsync(
                    process,
                    ProcessTerminationPolicy.ForceKill,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The execution failure or caller cancellation remains the primary failure.
        }
    }
}
