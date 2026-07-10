using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class RecordingDaemonPingClient : IDaemonPingClient
{
    private readonly Func<ResolvedUnityProjectContext, TimeSpan, string?, CancellationToken, ValueTask> handler;

    private readonly List<ResolvedUnityProjectContext> unityProjects = [];

    private readonly List<TimeSpan> timeouts = [];

    private readonly List<string?> sessionTokens = [];

    private readonly List<Invocation> invocations = [];

    public RecordingDaemonPingClient ()
        : this(static (_, _, _, _) => ValueTask.CompletedTask)
    {
    }

    public RecordingDaemonPingClient (
        Func<ResolvedUnityProjectContext, TimeSpan, string?, CancellationToken, ValueTask> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<ResolvedUnityProjectContext> UnityProjects => unityProjects;

    public IReadOnlyList<TimeSpan> Timeouts => timeouts;

    public IReadOnlyList<string?> SessionTokens => sessionTokens;

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask PingAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return RecordPing(
            unityProject,
            timeout,
            session: null,
            explicitSessionToken: null,
            cancellationToken);
    }

    public ValueTask PingSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return RecordPing(
            unityProject,
            timeout,
            session,
            explicitSessionToken: null,
            cancellationToken);
    }

    public ValueTask PingCanonicalEndpointWithTokenAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        return RecordPing(
            unityProject,
            timeout,
            session: null,
            sessionToken,
            cancellationToken);
    }

    private ValueTask RecordPing (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonSession? session,
        string? explicitSessionToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        var sessionToken = session?.SessionToken ?? explicitSessionToken;

        unityProjects.Add(unityProject);
        timeouts.Add(timeout);
        sessionTokens.Add(sessionToken);
        invocations.Add(new Invocation(
            unityProject,
            timeout,
            session,
            explicitSessionToken,
            cancellationToken));

        return handler(unityProject, timeout, sessionToken, cancellationToken);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        DaemonSession? Session,
        string? ExplicitSessionToken,
        CancellationToken CancellationToken)
    {
        public string? SessionToken => Session?.SessionToken ?? ExplicitSessionToken;
    }
}
