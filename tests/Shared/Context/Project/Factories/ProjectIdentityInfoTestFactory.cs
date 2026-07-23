using MackySoft.FileSystem;

namespace MackySoft.Ucli.TestSupport;

internal static class ProjectIdentityInfoTestFactory
{
    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public const string UnityVersion = "6000.1.4f1";

    public static string DefaultProjectPath { get; } = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "ucli-tests",
        "UnityProject"));

    public static ProjectIdentityInfo Create (
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion)
    {
        return CreateWithProjectPath(DefaultProjectPath, projectFingerprint, unityVersion);
    }

    public static ProjectIdentityInfo CreateWithProjectPath (
        string projectPath,
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion)
    {
        return ProjectIdentityInfo.From(ResolvedUnityProjectContext.Create(
            unityProjectRoot: AbsolutePath.Parse(projectPath),
            repositoryRoot: AbsolutePath.Parse(projectPath),
            projectFingerprint: projectFingerprint ?? ProjectFingerprint,
            pathSource: UnityProjectPathSource.CommandOption,
            pathSourceLabel: null,
            unityVersion: unityVersion));
    }

    public static ProjectIdentityInfo CreateRepositoryFixture (
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion)
    {
        return CreateWithProjectPath(
            projectPath: ProjectPathTestValues.RepositoryUnityProject,
            projectFingerprint: projectFingerprint,
            unityVersion: unityVersion);
    }

    public static ProjectIdentityInfo CreateForRepositoryRoot (
        string repositoryRoot,
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion)
    {
        return CreateWithProjectPath(
            projectPath: Path.Combine(repositoryRoot, "UnityProject"),
            projectFingerprint: projectFingerprint,
            unityVersion: unityVersion);
    }
}
