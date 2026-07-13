using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonCommandExecutionContextTestFactory
{
    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");

    public const string RepositoryRoot = "/tmp/repo-root";

    public const string UnityProjectRoot = "/tmp/unity-project";

    public const string UnityVersion = "6000.1.4f1";

    public static DaemonCommandExecutionContext Create (
        int timeoutMilliseconds,
        string repositoryRoot = RepositoryRoot,
        string unityProjectRoot = UnityProjectRoot,
        ProjectFingerprint? projectFingerprint = null,
        string unityVersion = UnityVersion,
        ConfigSource configSource = ConfigSource.Default)
    {
        return new DaemonCommandExecutionContext(
            Context: new ProjectContext(
                new ResolvedUnityProjectContext(
                    UnityProjectRoot: unityProjectRoot,
                    RepositoryRoot: repositoryRoot,
                    ProjectFingerprint: projectFingerprint ?? ProjectFingerprint,
                    PathSource: UnityProjectPathSource.CommandOption,
                    PathSourceLabel: null,
                    UnityVersion: unityVersion),
                UcliConfig.CreateDefault(),
                configSource),
            Timeout: TimeSpan.FromMilliseconds(timeoutMilliseconds));
    }
}
