namespace MackySoft.Ucli.Tests;

internal static class ResolvedUnityProjectContextTestFactory
{
    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public const string RepositoryRoot = "/repo";

    public const string UnityProjectRoot = "/repo/UnityProject";

    private const string DaemonLifecycleRepositoryRoot = "/tmp/repo-root";

    private const string DaemonLifecycleUnityProjectRoot = "/tmp/unity-project";

    public static ResolvedUnityProjectContext Create (
        string unityProjectRoot = UnityProjectRoot,
        string repositoryRoot = RepositoryRoot,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = ProjectIdentityDefaults.UnknownUnityVersion)
    {
        return ResolvedUnityProjectContext.Create(
            unityProjectRoot: unityProjectRoot,
            repositoryRoot: repositoryRoot,
            projectFingerprint: projectFingerprint ?? ProjectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateForRepositoryRoot (
        string repositoryRoot,
        ProjectFingerprint? projectFingerprint = null,
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
        ProjectFingerprint? projectFingerprint = null,
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
        ProjectFingerprint projectFingerprint,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption)
    {
        return Create(
            unityProjectRoot: DaemonLifecycleUnityProjectRoot,
            repositoryRoot: DaemonLifecycleRepositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource);
    }

}
