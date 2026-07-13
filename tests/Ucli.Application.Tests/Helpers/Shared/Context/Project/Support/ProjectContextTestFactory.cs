using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Tests;

internal static class ProjectContextTestFactory
{
    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public const string RepositoryRoot = "/workspace";

    public const string UnityProjectRoot = "/workspace/UnityProject";

    public const string UnityVersion = "6000.1.4f1";

    public const string PathSourceLabel = "--projectPath";

    private const string DaemonLifecycleRepositoryRoot = "/tmp/repo-root";

    private const string DaemonLifecycleUnityProjectRoot = "/tmp/unity-project";

    private const string RepositoryFixtureRoot = "/repo";

    private const string RepositoryFixtureUnityProjectRoot = "/repo/UnityProject";

    private const string TemporaryFixtureRepositoryRoot = "/tmp/repository";

    private const string TemporaryFixtureUnityProjectRoot = "/tmp/project";

    private static readonly string SingleRootUnityProjectRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));

    public static ProjectContext Create (
        UcliConfig? config = null,
        string unityProjectRoot = UnityProjectRoot,
        string repositoryRoot = RepositoryRoot,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = PathSourceLabel,
        string unityVersion = UnityVersion,
        ConfigSource configSource = ConfigSource.Default)
    {
        return new ProjectContext(
            CreateUnityProject(
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

    public static ResolvedUnityProjectContext CreateUnityProject (
        string unityProjectRoot = UnityProjectRoot,
        string repositoryRoot = RepositoryRoot,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = PathSourceLabel,
        string unityVersion = UnityVersion)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: unityProjectRoot,
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: projectFingerprint ?? ProjectFingerprint,
            PathSource: pathSource,
            PathSourceLabel: pathSourceLabel,
            UnityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateRepositoryFixtureUnityProject (
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null,
        string unityVersion = UnityVersion)
    {
        return CreateUnityProject(
            unityProjectRoot: RepositoryFixtureUnityProjectRoot,
            repositoryRoot: RepositoryFixtureRoot,
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
        return CreateUnityProject(
            unityProjectRoot: TemporaryFixtureUnityProjectRoot,
            repositoryRoot: TemporaryFixtureRepositoryRoot,
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
        return CreateUnityProject(
            unityProjectRoot: SingleRootUnityProjectRoot,
            repositoryRoot: SingleRootUnityProjectRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: unityVersion);
    }

    public static ResolvedUnityProjectContext CreateUnknownVersionUnityProject (
        string unityProjectRoot = RepositoryFixtureUnityProjectRoot,
        string repositoryRoot = RepositoryFixtureRoot,
        ProjectFingerprint? projectFingerprint = null,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption,
        string? pathSourceLabel = null)
    {
        return CreateUnityProject(
            unityProjectRoot: unityProjectRoot,
            repositoryRoot: repositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: pathSource,
            pathSourceLabel: pathSourceLabel,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
    }

    public static ResolvedUnityProjectContext CreateDaemonLifecycleUnityProject (
        ProjectFingerprint projectFingerprint,
        UnityProjectPathSource pathSource = UnityProjectPathSource.CommandOption)
    {
        return CreateUnityProject(
            unityProjectRoot: DaemonLifecycleUnityProjectRoot,
            repositoryRoot: DaemonLifecycleRepositoryRoot,
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
