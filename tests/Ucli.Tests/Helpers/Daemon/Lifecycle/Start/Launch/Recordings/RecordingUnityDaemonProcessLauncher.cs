using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingUnityDaemonProcessLauncher : IUnityDaemonProcessLauncher
{
    private readonly List<Invocation> invocations = [];

    public Action? OnLaunch { get; set; }

    public TimeSpan LaunchDelay { get; set; }

    public ManualTimeProvider? TimeProvider { get; set; }

    public UnityDaemonLaunchResult NextResult { get; set; } = UnityDaemonLaunchResult.Success(1000, DateTimeOffset.UtcNow);

    public IReadOnlyList<Invocation> Invocations => invocations;

    public async ValueTask<UnityDaemonLaunchResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        string daemonLogPath,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(unityProject, session, daemonLogPath, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        OnLaunch?.Invoke();
        if (LaunchDelay > TimeSpan.Zero)
        {
            if (TimeProvider != null)
            {
                TimeProvider.Advance(LaunchDelay);
            }
            else
            {
                throw new InvalidOperationException("ManualTimeProvider is required when LaunchDelay is configured.");
            }
        }

        return NextResult;
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession Session,
        string DaemonLogPath,
        CancellationToken CancellationToken);
}
