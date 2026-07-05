using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonShutdownClient : IDaemonShutdownClient
{
    private readonly List<Invocation> invocations = [];

    public DaemonShutdownAttemptResult NextResult { get; set; } = DaemonShutdownAttemptResult.Success();

    public TimeSpan Delay { get; set; }

    public ManualTimeProvider? TimeProvider { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonShutdownAttemptResult> SendShutdownAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(unityProject, session, timeout, cancellationToken));
        if (Delay > TimeSpan.Zero)
        {
            if (TimeProvider != null)
            {
                TimeProvider.Advance(Delay);
            }
            else
            {
                throw new InvalidOperationException("ManualTimeProvider is required when shutdown Delay is configured.");
            }
        }

        return ValueTask.FromResult(NextResult);
    }

    public readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession Session,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
