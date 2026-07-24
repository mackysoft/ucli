using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Represents a resolved UnityProject context shared by command foundation services. </summary>
internal sealed record ResolvedUnityProjectContext
{
    private ResolvedUnityProjectContext (
        AbsolutePath unityProjectRoot,
        AbsolutePath repositoryRoot,
        ProjectFingerprint projectFingerprint,
        UnityProjectPathSource pathSource,
        string? pathSourceLabel,
        string unityVersion)
    {
        UnityProjectRoot = unityProjectRoot;
        RepositoryRoot = repositoryRoot;
        ProjectFingerprint = projectFingerprint;
        PathSource = pathSource;
        PathSourceLabel = pathSourceLabel;
        UnityVersion = unityVersion;
    }

    /// <summary> Gets the normalized absolute UnityProject root path. </summary>
    public AbsolutePath UnityProjectRoot { get; }

    /// <summary> Gets the normalized absolute repository root path used for <c>.ucli</c> storage. </summary>
    public AbsolutePath RepositoryRoot { get; }

    /// <summary> Gets the deterministic fingerprint used for project identity checks. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the path source used during resolution. </summary>
    public UnityProjectPathSource PathSource { get; }

    /// <summary> Gets the optional label for the source used during resolution. </summary>
    public string? PathSourceLabel { get; }

    /// <summary> Gets the resolved Unity editor version, or <c>unknown</c>. </summary>
    public string UnityVersion { get; }

    /// <summary> Creates a resolved Unity project context with canonical absolute paths. </summary>
    /// <param name="unityProjectRoot"> The absolute Unity project root path. </param>
    /// <param name="repositoryRoot"> The absolute repository root path used for <c>.ucli</c> storage. </param>
    /// <param name="projectFingerprint"> The deterministic fingerprint for project identity checks. </param>
    /// <param name="pathSource"> The path source used during resolution. </param>
    /// <param name="pathSourceLabel"> The optional label for the source used during resolution. </param>
    /// <param name="unityVersion"> The Unity editor version resolved from <c>ProjectSettings/ProjectVersion.txt</c>, or <c>unknown</c>. </param>
    /// <returns> A context containing canonical absolute project and repository paths. </returns>
    public static ResolvedUnityProjectContext Create (
        AbsolutePath unityProjectRoot,
        AbsolutePath repositoryRoot,
        ProjectFingerprint projectFingerprint,
        UnityProjectPathSource pathSource,
        string? pathSourceLabel,
        string unityVersion)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(projectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityVersion);
        if (!string.Equals(unityVersion, unityVersion.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Unity version must not contain leading or trailing whitespace.", nameof(unityVersion));
        }

        if (!Enum.IsDefined(pathSource))
        {
            throw new ArgumentException("Unity project path source is not defined.", nameof(pathSource));
        }

        if (pathSourceLabel is not null
            && (string.IsNullOrWhiteSpace(pathSourceLabel)
                || !string.Equals(pathSourceLabel, pathSourceLabel.Trim(), StringComparison.Ordinal)))
        {
            throw new ArgumentException("Unity project path source label must be non-empty and must not contain outer whitespace when specified.", nameof(pathSourceLabel));
        }

        return new ResolvedUnityProjectContext(
            unityProjectRoot,
            repositoryRoot,
            projectFingerprint,
            pathSource,
            pathSourceLabel,
            unityVersion);
    }
}
