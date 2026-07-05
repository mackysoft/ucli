namespace MackySoft.Ucli.TestSupport;

internal static class ProjectIdentityInfoTestFactory
{
    public const string ProjectFingerprint = "project-fingerprint";

    public const string RepositoryFixtureProjectPath = "/repo/UnityProject";

    public const string UnityVersion = "6000.1.4f1";

    public static string DefaultProjectPath { get; } = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "ucli-tests",
        "UnityProject"));

    public static ProjectIdentityInfo Create (
        string? projectPath = null,
        string projectFingerprint = ProjectFingerprint,
        string unityVersion = UnityVersion)
    {
        return new ProjectIdentityInfo(
            ProjectPath: projectPath ?? DefaultProjectPath,
            ProjectFingerprint: projectFingerprint,
            UnityVersion: unityVersion);
    }

    public static ProjectIdentityInfo CreateRepositoryFixture (
        string projectFingerprint = ProjectFingerprint,
        string unityVersion = UnityVersion)
    {
        return Create(
            projectPath: RepositoryFixtureProjectPath,
            projectFingerprint: projectFingerprint,
            unityVersion: unityVersion);
    }

    public static ProjectIdentityInfo CreateForRepositoryRoot (
        string repositoryRoot,
        string projectFingerprint = ProjectFingerprint,
        string unityVersion = UnityVersion)
    {
        return Create(
            projectPath: Path.Combine(repositoryRoot, "UnityProject"),
            projectFingerprint: projectFingerprint,
            unityVersion: unityVersion);
    }
}
