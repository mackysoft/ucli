namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Represents the public project identity emitted by request command payloads. </summary>
/// <param name="ProjectPath"> The normalized absolute Unity project root path. </param>
/// <param name="ProjectFingerprint"> The resolved Unity project fingerprint. </param>
/// <param name="UnityVersion"> The Unity editor version resolved for the project, or <c>unknown</c>. </param>
internal sealed record ProjectIdentityInfo (
    string ProjectPath,
    string ProjectFingerprint,
    string UnityVersion)
{
    /// <summary> Creates public project identity from a resolved Unity project context. </summary>
    /// <param name="project"> The resolved Unity project context. </param>
    /// <returns> The normalized project identity. </returns>
    public static ProjectIdentityInfo From (ResolvedUnityProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new ProjectIdentityInfo(
            ProjectPath: project.UnityProjectRoot,
            ProjectFingerprint: project.ProjectFingerprint,
            UnityVersion: string.IsNullOrWhiteSpace(project.UnityVersion) ? ProjectIdentityDefaults.UnknownUnityVersion : project.UnityVersion);
    }
}
