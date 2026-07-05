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
        string? sessionToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);

        unityProjects.Add(unityProject);
        timeouts.Add(timeout);
        sessionTokens.Add(sessionToken);
        invocations.Add(new Invocation(unityProject, timeout, sessionToken, cancellationToken));

        return handler(unityProject, timeout, sessionToken, cancellationToken);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        string? SessionToken,
        CancellationToken CancellationToken);
}
