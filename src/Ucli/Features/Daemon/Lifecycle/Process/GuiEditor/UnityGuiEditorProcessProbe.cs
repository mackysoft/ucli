using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.EditorInstance;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.GuiEditor;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.GuiEditor;

/// <summary> Verifies Unity GUI Editor process candidates recorded in <c>EditorInstance.json</c>. </summary>
internal sealed class UnityGuiEditorProcessProbe : IUnityGuiEditorProcessProbe
{
    private const string BatchmodeArgument = "-batchmode";

    private const string ProjectPathArgument = "-projectPath";

    private const string UnityExecutableName = "Unity";

    private const string WindowsUnityExecutableName = "Unity.exe";

    private const string MacUnityBundleName = "Unity.app";

    private static readonly RootRelativePath UnityExecutablePathName =
        RootRelativePath.Parse(UnityExecutableName);

    private static readonly RootRelativePath WindowsUnityExecutablePathName =
        RootRelativePath.Parse(WindowsUnityExecutableName);

    private static readonly RootRelativePath MacUnityBundlePathName =
        RootRelativePath.Parse(MacUnityBundleName);

    private readonly IUnityGuiEditorProcessInspector processInspector;

    /// <summary> Initializes a new instance of the <see cref="UnityGuiEditorProcessProbe" /> class. </summary>
    public UnityGuiEditorProcessProbe (IUnityGuiEditorProcessInspector processInspector)
    {
        this.processInspector = processInspector ?? throw new ArgumentNullException(nameof(processInspector));
    }

    /// <inheritdoc />
    public ValueTask<UnityGuiEditorProcessProbeResult> ProbeAsync (
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

        if (inspection.StartTimeUtc is not DateTimeOffset processStartTimeUtc)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.Uncertain);
        }

        if (processStartTimeUtc > marker.UpdatedAtUtc)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.StaleMarker);
        }

        if (string.IsNullOrWhiteSpace(inspection.CommandLine))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.Uncertain);
        }

        if (ContainsBatchmodeArgument(inspection.CommandLine))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.Batchmode);
        }

        if (inspection.IsOwnedByCurrentUser is not true)
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(
                inspection.IsOwnedByCurrentUser == false
                    ? UnityGuiEditorProcessProbeStatus.DifferentUser
                    : UnityGuiEditorProcessProbeStatus.Uncertain);
        }

        if (!LooksLikeUnityEditorProcess(inspection, marker))
        {
            return UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.NotUnityEditor);
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
        return marker.AppPath is not null
            || marker.AppContentsPath is not null;
    }

    private static bool MatchesMarkerPath (
        AbsolutePath? executablePath,
        AbsolutePath? markerPath)
    {
        if (executablePath is null || markerPath is null)
        {
            return false;
        }

        if (!IsSpecificUnityApplicationPath(markerPath))
        {
            return false;
        }

        return markerPath.IsSameOrAncestorOf(executablePath);
    }

    private static bool IsSpecificUnityApplicationPath (AbsolutePath normalizedMarkerPath)
    {
        return HasPathSegment(normalizedMarkerPath, MacUnityBundlePathName)
            || HasUnityExecutableFileName(normalizedMarkerPath);
    }

    private static bool LooksLikeUnityEditorProcessMetadata (UnityGuiEditorProcessInspection inspection)
    {
        if (inspection.ExecutablePath is not null
            && HasUnityExecutableFileName(inspection.ExecutablePath))
        {
            return true;
        }

        return string.Equals(inspection.ProcessName, UnityExecutableName, StringComparison.OrdinalIgnoreCase)
            && ContainsProjectPathArgument(inspection.CommandLine);
    }

    private static bool HasPathSegment (
        AbsolutePath path,
        RootRelativePath expectedSegment)
    {
        ArgumentNullException.ThrowIfNull(path);
        var currentPath = path;
        while (currentPath.TryGetParent(out var parentPath))
        {
            var currentSegment = ContainedPath.Create(
                parentPath,
                currentPath).RelativePath;
            if (currentSegment == expectedSegment)
            {
                return true;
            }

            currentPath = parentPath;
        }

        return false;
    }

    private static bool HasUnityExecutableFileName (AbsolutePath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!path.TryGetParent(out var parentPath))
        {
            return false;
        }

        var fileName = ContainedPath.Create(
            parentPath,
            path).RelativePath;
        return fileName == UnityExecutablePathName
            || fileName == WindowsUnityExecutablePathName;
    }

    private static bool ContainsProjectPathArgument (string? commandLine)
    {
        return commandLine != null
            && commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(static x => string.Equals(x, ProjectPathArgument, StringComparison.Ordinal));
    }
}
