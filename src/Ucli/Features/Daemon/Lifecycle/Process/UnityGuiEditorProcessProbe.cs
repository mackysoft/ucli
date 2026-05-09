using System.Runtime.InteropServices;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Verifies Unity GUI Editor process candidates recorded in <c>EditorInstance.json</c>. </summary>
internal sealed class UnityGuiEditorProcessProbe : IUnityGuiEditorProcessProbe
{
    private const string BatchmodeArgument = "-batchmode";

    private const string ProjectPathArgument = "-projectPath";

    private const string UnityExecutableName = "Unity";

    private const string WindowsUnityExecutableName = "Unity.exe";

    private const string MacUnityBundleName = "Unity.app";

    private readonly IUnityGuiEditorProcessInspector processInspector;

    /// <summary> Initializes a new instance of the <see cref="UnityGuiEditorProcessProbe" /> class. </summary>
    public UnityGuiEditorProcessProbe (IUnityGuiEditorProcessInspector processInspector)
    {
        this.processInspector = processInspector ?? throw new ArgumentNullException(nameof(processInspector));
    }

    /// <inheritdoc />
    public ValueTask<UnityGuiEditorProcessProbeResult> Probe (
        UnityEditorInstanceMarker marker,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(marker);

        return ValueTask.FromResult(ProbeInspection(processInspector.Inspect(marker.ProcessId), marker));
    }

    private static UnityGuiEditorProcessProbeResult ProbeInspection (
        UnityGuiEditorProcessInspection inspection,
        UnityEditorInstanceMarker marker)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        ArgumentNullException.ThrowIfNull(marker);

        if (!inspection.Exists || inspection.HasExited)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.NotRunning);
        }

        if (inspection.Error != null)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.Uncertain,
                inspection.StartTimeUtc,
                inspection.Error);
        }

        if (inspection.StartTimeUtc is not DateTimeOffset processStartTimeUtc)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.Uncertain,
                error: ExecutionError.InternalError(
                    $"Failed to read Unity GUI Editor process start time. ProcessId={marker.ProcessId}."));
        }

        if (processStartTimeUtc > marker.UpdatedAtUtc)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.StaleMarker,
                processStartTimeUtc);
        }

        if (string.IsNullOrWhiteSpace(inspection.CommandLine))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.Uncertain,
                processStartTimeUtc,
                ExecutionError.InternalError(
                    $"Failed to read Unity GUI Editor process command line. ProcessId={marker.ProcessId}."));
        }

        if (ContainsBatchmodeArgument(inspection.CommandLine))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.Batchmode,
                processStartTimeUtc);
        }

        if (inspection.IsOwnedByCurrentUser is not true)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                inspection.IsOwnedByCurrentUser == false
                    ? UnityGuiEditorProcessProbeStatus.DifferentUser
                    : UnityGuiEditorProcessProbeStatus.Uncertain,
                processStartTimeUtc);
        }

        if (!LooksLikeUnityEditorProcess(inspection, marker))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.NotUnityEditor,
                processStartTimeUtc);
        }

        return UnityGuiEditorProcessProbeResult.Matching(processStartTimeUtc);
    }

    private static bool ContainsBatchmodeArgument (string? commandLine)
    {
        return commandLine != null
            && commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(static x => string.Equals(x, BatchmodeArgument, StringComparison.Ordinal));
    }

    private static bool LooksLikeUnityEditorProcess (
        UnityGuiEditorProcessInspection inspection,
        UnityEditorInstanceMarker marker)
    {
        if (HasMarkerApplicationPath(marker))
        {
            return MatchesMarkerPath(inspection.ExecutablePath, marker.AppPath)
                || MatchesMarkerPath(inspection.ExecutablePath, marker.AppContentsPath);
        }

        return LooksLikeUnityEditorProcessMetadata(inspection);
    }

    private static bool HasMarkerApplicationPath (UnityEditorInstanceMarker marker)
    {
        return !string.IsNullOrWhiteSpace(marker.AppPath)
            || !string.IsNullOrWhiteSpace(marker.AppContentsPath);
    }

    private static bool MatchesMarkerPath (
        string? executablePath,
        string? markerPath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || string.IsNullOrWhiteSpace(markerPath))
        {
            return false;
        }

        var normalizedExecutablePath = NormalizePath(executablePath);
        var normalizedMarkerPath = NormalizePath(markerPath);
        if (normalizedExecutablePath == null || normalizedMarkerPath == null)
        {
            return false;
        }

        if (!IsSpecificUnityApplicationPath(normalizedMarkerPath))
        {
            return false;
        }

        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(normalizedExecutablePath, normalizedMarkerPath, comparison))
        {
            return true;
        }

        return normalizedExecutablePath.Length > normalizedMarkerPath.Length
            && normalizedExecutablePath.StartsWith(normalizedMarkerPath, comparison)
            && IsDirectorySeparator(normalizedExecutablePath[normalizedMarkerPath.Length]);
    }

    private static string? NormalizePath (string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            return null;
        }

        try
        {
            var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrWhiteSpace(normalizedPath)
                ? null
                : normalizedPath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsDirectorySeparator (char value)
    {
        return value == Path.DirectorySeparatorChar
            || value == Path.AltDirectorySeparatorChar;
    }

    private static bool IsSpecificUnityApplicationPath (string normalizedMarkerPath)
    {
        return HasPathSegment(normalizedMarkerPath, MacUnityBundleName)
            || HasUnityExecutableFileName(normalizedMarkerPath);
    }

    private static bool LooksLikeUnityEditorProcessMetadata (UnityGuiEditorProcessInspection inspection)
    {
        if (HasUnityExecutableFileName(inspection.ExecutablePath))
        {
            return true;
        }

        return string.Equals(inspection.ProcessName, UnityExecutableName, StringComparison.OrdinalIgnoreCase)
            && ContainsProjectPathArgument(inspection.CommandLine);
    }

    private static bool HasPathSegment (
        string? path,
        string segment)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(value => string.Equals(value, segment, GetPathComparison()));
    }

    private static bool HasUnityExecutableFileName (string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        return string.Equals(fileName, UnityExecutableName, GetPathComparison())
            || string.Equals(fileName, WindowsUnityExecutableName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsProjectPathArgument (string? commandLine)
    {
        return commandLine != null
            && commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(static x => string.Equals(x, ProjectPathArgument, StringComparison.Ordinal));
    }

    private static StringComparison GetPathComparison ()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
