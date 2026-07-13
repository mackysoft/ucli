namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Represents a resolved UnityProject context shared by command foundation services. </summary>
internal sealed record ResolvedUnityProjectContext
{
    /// <summary> Initializes a validated resolved UnityProject context. </summary>
    /// <param name="UnityProjectRoot"> The normalized absolute UnityProject root path. </param>
    /// <param name="RepositoryRoot"> The normalized absolute repository root path used for <c>.ucli</c> storage. </param>
    /// <param name="ProjectFingerprint"> The deterministic fingerprint for project identity checks. </param>
    /// <param name="PathSource"> The path source used during resolution. </param>
    /// <param name="PathSourceLabel"> The optional label for the source used during resolution. </param>
    /// <param name="UnityVersion"> The Unity editor version resolved from <c>ProjectSettings/ProjectVersion.txt</c>, or <c>unknown</c>. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="ProjectFingerprint" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when a required text value is empty or <paramref name="PathSource" /> is undefined. </exception>
    public ResolvedUnityProjectContext (
        string UnityProjectRoot,
        string RepositoryRoot,
        ProjectFingerprint ProjectFingerprint,
        UnityProjectPathSource PathSource,
        string? PathSourceLabel = null,
        string UnityVersion = ProjectIdentityDefaults.UnknownUnityVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(UnityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(RepositoryRoot);
        ArgumentNullException.ThrowIfNull(ProjectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(UnityVersion);

        if (!Enum.IsDefined(PathSource))
        {
            throw new ArgumentException("Unity project path source is not defined.", nameof(PathSource));
        }

        if (PathSourceLabel is not null && string.IsNullOrWhiteSpace(PathSourceLabel))
        {
            throw new ArgumentException("Unity project path source label must not be empty when specified.", nameof(PathSourceLabel));
        }

        this.UnityProjectRoot = UnityProjectRoot;
        this.RepositoryRoot = RepositoryRoot;
        this.ProjectFingerprint = ProjectFingerprint;
        this.PathSource = PathSource;
        this.PathSourceLabel = PathSourceLabel;
        this.UnityVersion = UnityVersion;
    }

    /// <summary> Gets the normalized absolute UnityProject root path. </summary>
    public string UnityProjectRoot { get; }

    /// <summary> Gets the normalized absolute repository root path used for <c>.ucli</c> storage. </summary>
    public string RepositoryRoot { get; }

    /// <summary> Gets the deterministic fingerprint used for project identity checks. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the path source used during resolution. </summary>
    public UnityProjectPathSource PathSource { get; }

    /// <summary> Gets the optional label for the source used during resolution. </summary>
    public string? PathSourceLabel { get; }

    /// <summary> Gets the resolved Unity editor version, or <c>unknown</c>. </summary>
    public string UnityVersion { get; }
}
