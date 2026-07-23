using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingUnityDaemonProcessLauncher : IUnityDaemonProcessLauncher
{
    private readonly List<Invocation> invocations = [];

    public Action? OnLaunch { get; set; }

    public TimeSpan LaunchDelay { get; set; }

    public Func<ResolvedUnityProjectContext, DaemonSession, AbsolutePath, CancellationToken, ValueTask<UnityDaemonLaunchResult>>? Handler { get; set; }

    public ManualTimeProvider? TimeProvider { get; set; }

    public UnityDaemonLaunchResult? NextResult { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public async ValueTask<UnityDaemonLaunchResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        AbsolutePath daemonLogPath,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(unityProject, session, daemonLogPath, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        OnLaunch?.Invoke();
        if (Handler is not null)
        {
            return await Handler(unityProject, session, daemonLogPath, cancellationToken).ConfigureAwait(false);
        }

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

        return NextResult
            ?? throw new InvalidOperationException("A daemon process launch result must be configured before launch.");
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession Session,
        AbsolutePath DaemonLogPath,
        CancellationToken CancellationToken);
}
