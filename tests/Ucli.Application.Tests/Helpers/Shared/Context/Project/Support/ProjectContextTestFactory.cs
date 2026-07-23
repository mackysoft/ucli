using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Tests;

internal static class ProjectContextTestFactory
{
    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public static string RepositoryRoot { get; } = ProjectPathTestValues.WorkspaceRoot;

    public static string UnityProjectRoot { get; } = ProjectPathTestValues.WorkspaceUnityProject;

    public const string UnityVersion = "6000.1.4f1";

    public const string PathSourceLabel = "--projectPath";

    private static readonly string SingleRootUnityProjectRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));

    public static ProjectContext Create (
        UcliConfig? config = null,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = PathSourceLabel,
        string unityVersion = UnityVersion,
        ConfigSource configSource = ConfigSource.Default)
    {
        return CreateWithPaths(
            UnityProjectRoot,
            RepositoryRoot,
            config,
            projectFingerprint,
            pathSource,
            pathSourceLabel,
            unityVersion,
            configSource);
    }

    public static ProjectContext CreateWithPaths (
        string unityProjectRoot,
        string repositoryRoot,
        UcliConfig? config = null,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = PathSourceLabel,
        string unityVersion = UnityVersion,
        ConfigSource configSource = ConfigSource.Default)
    {
        return new ProjectContext(
            CreateUnityProjectWithPaths(
                unityProjectRoot: unityProjectRoot,
                repositoryRoot: repositoryRoot,
                projectFingerprint: projectFingerprint,
                pathSource: pathSource,
                pathSourceLabel: pathSourceLabel,
                unityVersion: unityVersion),
            config ?? UcliConfig.CreateDefault(),
            configSource);
    }

    public static ProjectContext CreateRepositoryFixtureProject (
        UcliConfig? config = null,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = UnityVersion,
        ConfigSource configSource = ConfigSource.Default)
    {
        return new ProjectContext(
            CreateRepositoryFixtureUnityProject(
                projectFingerprint: projectFingerprint,
                pathSource: pathSource,
                pathSourceLabel: pathSourceLabel,
                unityVersion: unityVersion),
            config ?? UcliConfig.CreateDefault(),
            configSource);
    }

    public static ProjectContext CreateSingleRootProject (
        UcliConfig? config = null,
        ProjectFingerprint? projectFingerprint = null,
        string? pathSourceLabel = null,
        string unityVersion = UnityVersion,
        ConfigSource configSource = ConfigSource.Default)
    {
        return new ProjectContext(
            CreateSingleRootUnityProject(
                projectFingerprint: projectFingerprint,
                pathSourceLabel: pathSourceLabel,
                unityVersion: unityVersion),
            config ?? UcliConfig.CreateDefault(),
            configSource);
    }

    public static ProjectContext CreateTemporaryFixtureProject (
        UcliConfig? config = null,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = UnityVersion,
        ConfigSource configSource = ConfigSource.Default)
    {
        return new ProjectContext(
            CreateTemporaryFixtureUnityProject(
                projectFingerprint: projectFingerprint,
                pathSource: pathSource,
                pathSourceLabel: pathSourceLabel,
                unityVersion: unityVersion),
            config ?? UcliConfig.CreateDefault(),
            configSource);
    }

    public static ResolvedUnityProjectContext CreateUnityProjectWithPaths (
        string unityProjectRoot,
        string repositoryRoot,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = PathSourceLabel,
        string unityVersion = UnityVersion)
    {
        return ResolvedUnityProjectContext.Create(
            unityProjectRoot: AbsolutePath.Parse(unityProjectRoot),
            repositoryRoot: AbsolutePath.Parse(repositoryRoot),
            projectFingerprint: projectFingerprint ?? ProjectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateRepositoryFixtureUnityProject (
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = UnityVersion)
    {
        return CreateUnityProjectWithPaths(
            unityProjectRoot: ProjectPathTestValues.RepositoryUnityProject,
            repositoryRoot: ProjectPathTestValues.RepositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateTemporaryFixtureUnityProject (
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = UnityVersion)
    {
        return CreateUnityProjectWithPaths(
            unityProjectRoot: ProjectPathTestValues.TemporaryUnityProject,
            repositoryRoot: ProjectPathTestValues.TemporaryRepositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateSingleRootUnityProject (
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = UnityVersion)
    {
        return CreateUnityProjectWithPaths(
            unityProjectRoot: SingleRootUnityProjectRoot,
            repositoryRoot: SingleRootUnityProjectRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateUnknownVersionUnityProject (
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null)
    {
        return CreateUnityProjectWithPaths(
            unityProjectRoot: ProjectPathTestValues.RepositoryUnityProject,
            repositoryRoot: ProjectPathTestValues.RepositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
    }

    public static ResolvedUnityProjectContext CreateDaemonLifecycleUnityProject (
        ProjectFingerprint projectFingerprint,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption)
    {
        return CreateUnityProjectWithPaths(
            unityProjectRoot: ProjectPathTestValues.TemporaryUnityProject,
            repositoryRoot: ProjectPathTestValues.TemporaryRepositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: null);
    }

    public static ProjectContext CreateDaemonLifecycleProject (
        UcliConfig? config = null,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        ConfigSource configSource = ConfigSource.Default)
    {
        return new ProjectContext(
            CreateDaemonLifecycleUnityProject(
                projectFingerprint: projectFingerprint ?? ProjectFingerprint,
                pathSource: pathSource),
            config ?? UcliConfig.CreateDefault(),
            configSource);
    }
}
