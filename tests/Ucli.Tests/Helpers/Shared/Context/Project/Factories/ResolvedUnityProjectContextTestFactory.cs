using MackySoft.FileSystem;

namespace MackySoft.Ucli.Tests;

internal static class ResolvedUnityProjectContextTestFactory
{
    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public static string RepositoryRoot { get; } = ProjectPathTestValues.RepositoryRoot;

    public static string UnityProjectRoot { get; } = ProjectPathTestValues.RepositoryUnityProject;

    public static ResolvedUnityProjectContext Create (
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = ProjectIdentityDefaults.UnknownUnityVersion)
    {
        return CreateWithPaths(
            UnityProjectRoot,
            RepositoryRoot,
            projectFingerprint,
            pathSource,
            pathSourceLabel,
            unityVersion);
    }

    public static ResolvedUnityProjectContext CreateWithPaths (
        string unityProjectRoot,
        string repositoryRoot,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = ProjectIdentityDefaults.UnknownUnityVersion)
    {
        return ResolvedUnityProjectContext.Create(
            unityProjectRoot: AbsolutePath.Parse(unityProjectRoot),
            repositoryRoot: AbsolutePath.Parse(repositoryRoot),
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
        return CreateWithPaths(
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
        return CreateWithPaths(
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
        return CreateWithPaths(
            unityProjectRoot: ProjectPathTestValues.TemporaryUnityProject,
            repositoryRoot: ProjectPathTestValues.TemporaryRepositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource);
    }

}
