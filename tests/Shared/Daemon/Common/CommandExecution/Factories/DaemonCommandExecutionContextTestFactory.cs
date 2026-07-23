using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;

using MackySoft.FileSystem;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonCommandExecutionContextTestFactory
{
    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");

    public static string RepositoryRoot { get; } = ProjectPathTestValues.TemporaryRepositoryRoot;

    public static string UnityProjectRoot { get; } = ProjectPathTestValues.TemporaryUnityProject;

    public const string UnityVersion = "6000.1.4f1";

    public static DaemonCommandExecutionContext Create (
        int timeoutMilliseconds,
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion,
        ConfigSource configSource = ConfigSource.Default)
    {
        return CreateForRepositoryRoot(
            timeoutMilliseconds,
            RepositoryRoot,
            projectFingerprint,
            unityVersion,
            configSource);
    }

    public static DaemonCommandExecutionContext CreateForRepositoryRoot (
        int timeoutMilliseconds,
        string repositoryRoot,
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion,
        ConfigSource configSource = ConfigSource.Default)
    {
        return new DaemonCommandExecutionContext(
            Context: new ProjectContext(
                ResolvedUnityProjectContext.Create(
                    unityProjectRoot: AbsolutePath.Parse(Path.Combine(repositoryRoot, "UnityProject")),
                    repositoryRoot: AbsolutePath.Parse(repositoryRoot),
                    projectFingerprint: projectFingerprint ?? ProjectFingerprint,
                    pathSource: UnityProjectPathSource.CommandOption,
                    pathSourceLabel: null,
                    unityVersion: unityVersion),
                UcliConfig.CreateDefault(),
                configSource),
            Timeout: TimeSpan.FromMilliseconds(timeoutMilliseconds));
    }
}
