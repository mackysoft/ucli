using System.ComponentModel;
using System.Diagnostics;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Executes external helper processes used during supervisor bootstrap. </summary>
internal sealed class SupervisorExternalProcessRunner
{
    /// <summary> Executes one external process and captures its standard streams. </summary>
    /// <param name="fileName"> The executable file name. </param>
    /// <param name="arguments"> The argument list passed to the process. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The captured process execution result. </returns>
    public async ValueTask<SupervisorExternalProcessExecutionResult> Run (
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
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        var standardError = await standardErrorTask.ConfigureAwait(false);
        return new SupervisorExternalProcessExecutionResult(process.ExitCode, standardOutput, standardError);
    }

    /// <summary> Executes one external process and suppresses any bootstrap-recovery failure. </summary>
    /// <param name="fileName"> The executable file name. </param>
    /// <param name="arguments"> The argument list passed to the process. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    public async ValueTask RunIgnoringExitCode (
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Run(fileName, arguments, cancellationToken).ConfigureAwait(false);
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
}
