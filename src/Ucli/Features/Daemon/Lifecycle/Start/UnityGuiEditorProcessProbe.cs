using System.Runtime.InteropServices;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Shared.Foundation;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start;

/// <summary> Verifies Unity GUI Editor process candidates recorded in <c>EditorInstance.json</c>. </summary>
internal sealed class UnityGuiEditorProcessProbe : IUnityGuiEditorProcessProbe
{
    private const string BatchmodeArgument = "-batchmode";

    private const string UnityProcessNameFragment = "unity";

    /// <inheritdoc />
    public ValueTask<UnityGuiEditorProcessProbeResult> Probe (
        UnityEditorInstanceMarker marker,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(marker);

        DiagnosticsProcess process;
        try
        {
            process = DiagnosticsProcess.GetProcessById(marker.ProcessId);
        }
        catch (ArgumentException)
        {
            return ValueTask.FromResult(UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.NotRunning));
        }

        using (process)
        {
            return ValueTask.FromResult(ProbeResolvedProcess(process, marker));
        }
    }

    private static UnityGuiEditorProcessProbeResult ProbeResolvedProcess (
        DiagnosticsProcess process,
        UnityEditorInstanceMarker marker)
    {
        if (HasExited(process))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.NotRunning);
        }

        DateTimeOffset processStartTimeUtc;
        try
        {
            processStartTimeUtc = process.StartTime.ToUniversalTime();
        }
        catch (InvalidOperationException) when (HasExited(process))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.NotRunning);
        }
        catch (Exception exception)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.Uncertain,
                error: ExecutionError.InternalError(
                    $"Failed to read Unity GUI Editor process start time. ProcessId={marker.ProcessId}. {exception.Message}"));
        }

        if (processStartTimeUtc > marker.UpdatedAtUtc)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.StaleMarker,
                processStartTimeUtc);
        }

        var commandLine = TryReadCommandLine(process.Id);
        if (ContainsBatchmodeArgument(commandLine))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.Batchmode,
                processStartTimeUtc);
        }

        if (!IsSameUser(process.Id))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.DifferentUser,
                processStartTimeUtc);
        }

        if (!LooksLikeUnityEditorProcess(process, marker, commandLine))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.NotUnityEditor,
                processStartTimeUtc);
        }

        return UnityGuiEditorProcessProbeResult.Matching(processStartTimeUtc);
    }

    private static bool HasExited (DiagnosticsProcess process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool ContainsBatchmodeArgument (string? commandLine)
    {
        return commandLine != null
            && commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(static x => string.Equals(x, BatchmodeArgument, StringComparison.Ordinal));
    }

    private static bool LooksLikeUnityEditorProcess (
        DiagnosticsProcess process,
        UnityEditorInstanceMarker marker,
        string? commandLine)
    {
        if (ContainsUnityFragment(process.ProcessName))
        {
            return true;
        }

        if (ContainsUnityFragment(commandLine))
        {
            return true;
        }

        var executablePath = TryReadExecutablePath(process);
        if (MatchesMarkerPath(executablePath, marker.AppPath)
            || MatchesMarkerPath(executablePath, marker.AppContentsPath)
            || ContainsUnityFragment(executablePath))
        {
            return true;
        }

        return false;
    }

    private static string? TryReadExecutablePath (DiagnosticsProcess process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception) when (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool MatchesMarkerPath (
        string? executablePath,
        string? markerPath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || string.IsNullOrWhiteSpace(markerPath))
        {
            return false;
        }

        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return executablePath.StartsWith(markerPath, comparison)
            || markerPath.StartsWith(executablePath, comparison);
    }

    private static bool ContainsUnityFragment (string? value)
    {
        return value?.IndexOf(UnityProcessNameFragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSameUser (int processId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }

        var currentUser = Environment.UserName;
        if (string.IsNullOrWhiteSpace(currentUser))
        {
            return false;
        }

        var processUser = TryReadUnixProcessUser(processId);
        return string.Equals(processUser, currentUser, StringComparison.Ordinal);
    }

    private static string? TryReadUnixProcessUser (int processId)
    {
        try
        {
            using var process = DiagnosticsProcess.Start(new DiagnosticsProcessStartInfo
            {
                FileName = "ps",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                ArgumentList =
                {
                    "-p",
                    processId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "-o",
                    "user=",
                },
            });
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);
            return process.ExitCode == 0
                ? output.Trim()
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryReadCommandLine (int processId)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }

            using var process = DiagnosticsProcess.Start(new DiagnosticsProcessStartInfo
            {
                FileName = "ps",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                ArgumentList =
                {
                    "-p",
                    processId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "-o",
                    "args=",
                },
            });
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);
            return process.ExitCode == 0
                ? output.Trim()
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
