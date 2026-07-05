using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

internal static class ResolvedUnityProjectContextTestFactory
{
    public const string ProjectFingerprint = "project-fingerprint";

    public const string RepositoryRoot = "/repo";

    public const string UnityProjectRoot = "/repo/UnityProject";

    private const string DaemonLifecycleRepositoryRoot = "/tmp/repo-root";

    private const string DaemonLifecycleUnityProjectRoot = "/tmp/unity-project";

    public static ResolvedUnityProjectContext Create (
        string unityProjectRoot = UnityProjectRoot,
        string repositoryRoot = RepositoryRoot,
        string projectFingerprint = ProjectFingerprint,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = ProjectIdentityDefaults.UnknownUnityVersion)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: unityProjectRoot,
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: projectFingerprint,
            PathSource: pathSource,
            PathSourceLabel: pathSourceLabel,
            UnityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateForRepositoryRoot (
        string repositoryRoot,
        string projectFingerprint = ProjectFingerprint,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = ProjectIdentityDefaults.UnknownUnityVersion)
    {
        return Create(
            unityProjectRoot: Path.Combine(repositoryRoot, "UnityProject"),
            repositoryRoot: repositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateWithUnityProjectDirectory (
        TestDirectoryScope scope,
        string projectFingerprint = ProjectFingerprint,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = ProjectIdentityDefaults.UnknownUnityVersion)
    {
        return Create(
            unityProjectRoot: scope.CreateDirectory("UnityProject"),
            repositoryRoot: scope.FullPath,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateDaemonLifecycleContext (
        string projectFingerprint,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption)
    {
        return Create(
            unityProjectRoot: DaemonLifecycleUnityProjectRoot,
            repositoryRoot: DaemonLifecycleRepositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource);
    }

}
