using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Represents the public project identity emitted by request command payloads. </summary>
internal sealed record ProjectIdentityInfo
{
    private ProjectIdentityInfo (
        string projectPath,
        ProjectFingerprint projectFingerprint,
        string unityVersion)
    {
        ProjectPath = projectPath;
        ProjectFingerprint = projectFingerprint;
        UnityVersion = unityVersion;
    }

    /// <summary> Gets the normalized absolute Unity project root path. </summary>
    public string ProjectPath { get; }

    /// <summary> Gets the resolved Unity project fingerprint. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the resolved Unity editor version, or <c>unknown</c>. </summary>
    public string UnityVersion { get; }

    /// <summary> Creates public project identity from a resolved Unity project context. </summary>
    /// <param name="project"> The resolved Unity project context. </param>
    /// <returns> The normalized project identity. </returns>
    public static ProjectIdentityInfo From (ResolvedUnityProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new ProjectIdentityInfo(
            project.UnityProjectRoot,
            project.ProjectFingerprint,
            project.UnityVersion);
    }

    /// <summary> Validates a host-reported identity against the requested project and creates the canonical public identity. </summary>
    /// <param name="expectedProject"> The locally resolved project targeted by the request. </param>
    /// <param name="hostProject"> The project identity reported by the Unity host. </param>
    /// <param name="project"> The canonical public identity when validation succeeds; otherwise <see langword="null" />. </param>
    /// <param name="mismatchKind"> The mismatched identity component when validation fails; otherwise unspecified. </param>
    /// <returns> <see langword="true" /> when the host identity belongs to the requested project; otherwise <see langword="false" />. </returns>
    public static bool TryFromHost (
        ResolvedUnityProjectContext expectedProject,
        IpcProjectIdentity hostProject,
        [NotNullWhen(true)] out ProjectIdentityInfo? project,
        out ProjectIdentityMismatchKind mismatchKind)
    {
        ArgumentNullException.ThrowIfNull(expectedProject);
        ArgumentNullException.ThrowIfNull(hostProject);

        project = null;
        if (hostProject.ProjectFingerprint != expectedProject.ProjectFingerprint)
        {
            mismatchKind = ProjectIdentityMismatchKind.ProjectFingerprint;
            return false;
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(hostProject.ProjectPath, expectedProject.UnityProjectRoot, pathComparison))
        {
            mismatchKind = ProjectIdentityMismatchKind.ProjectPath;
            return false;
        }

        var expectedUnityVersion = expectedProject.UnityVersion;
        if (!string.Equals(expectedUnityVersion, ProjectIdentityDefaults.UnknownUnityVersion, StringComparison.Ordinal)
            && !string.Equals(hostProject.UnityVersion, expectedUnityVersion, StringComparison.Ordinal))
        {
            mismatchKind = ProjectIdentityMismatchKind.UnityVersion;
            return false;
        }

        project = new ProjectIdentityInfo(
            expectedProject.UnityProjectRoot,
            expectedProject.ProjectFingerprint,
            string.Equals(expectedUnityVersion, ProjectIdentityDefaults.UnknownUnityVersion, StringComparison.Ordinal)
                ? hostProject.UnityVersion
                : expectedUnityVersion);
        mismatchKind = default;
        return true;
    }

}
