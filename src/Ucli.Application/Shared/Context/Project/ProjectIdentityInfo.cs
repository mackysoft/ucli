namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Represents the public project identity emitted by request command payloads. </summary>
internal sealed record ProjectIdentityInfo
{
    /// <summary> Initializes a validated public project identity. </summary>
    /// <param name="ProjectPath"> The normalized absolute Unity project root path. </param>
    /// <param name="ProjectFingerprint"> The resolved Unity project fingerprint. </param>
    /// <param name="UnityVersion"> The Unity editor version resolved for the project, or <c>unknown</c>. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="ProjectFingerprint" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when a required text value is empty. </exception>
    public ProjectIdentityInfo (
        string ProjectPath,
        ProjectFingerprint ProjectFingerprint,
        string UnityVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ProjectPath);
        ArgumentNullException.ThrowIfNull(ProjectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(UnityVersion);

        this.ProjectPath = ProjectPath;
        this.ProjectFingerprint = ProjectFingerprint;
        this.UnityVersion = UnityVersion;
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
            ProjectPath: project.UnityProjectRoot,
            ProjectFingerprint: project.ProjectFingerprint,
            UnityVersion: project.UnityVersion);
    }
}
