namespace MackySoft.Ucli.TestSupport;

internal static class ProjectIdentityInfoTestFactory
{
    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public const string RepositoryFixtureProjectPath = "/repo/UnityProject";

    public const string UnityVersion = "6000.1.4f1";

    public static string DefaultProjectPath { get; } = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "ucli-tests",
        "UnityProject"));

    public static ProjectIdentityInfo Create (
        string? projectPath = null,
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion)
    {
        var resolvedProjectPath = projectPath ?? DefaultProjectPath;
        return ProjectIdentityInfo.From(ResolvedUnityProjectContext.Create(
            unityProjectRoot: resolvedProjectPath,
            repositoryRoot: resolvedProjectPath,
            projectFingerprint: projectFingerprint ?? ProjectFingerprint,
            pathSource: UnityProjectPathSource.CommandOption,
            pathSourceLabel: null,
            unityVersion: unityVersion));
    }

    public static ProjectIdentityInfo CreateRepositoryFixture (
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion)
    {
        return Create(
            projectPath: RepositoryFixtureProjectPath,
            projectFingerprint: projectFingerprint,
            unityVersion: unityVersion);
    }

    public static ProjectIdentityInfo CreateForRepositoryRoot (
        string repositoryRoot,
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion)
    {
        return Create(
            projectPath: Path.Combine(repositoryRoot, "UnityProject"),
            projectFingerprint: projectFingerprint,
            unityVersion: unityVersion);
    }
}
