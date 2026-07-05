using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonLogsClient : IDaemonLogsClient
{
    private readonly Queue<DaemonLogsClientReadResult> responses;
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonLogsClient (IEnumerable<DaemonLogsClientReadResult> responses)
    {
        this.responses = new Queue<DaemonLogsClientReadResult>(responses);
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonLogsClientReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        IpcDaemonLogsReadRequest query,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(unityProject, query, timeout, cancellationToken));

        if (responses.Count == 0)
        {
            return ValueTask.FromResult(DaemonLogsClientReadResult.Success(
                new IpcDaemonLogsReadResponse(Array.Empty<IpcDaemonLogEvent>(), query.After ?? "stream-1:1")));
        }

        return ValueTask.FromResult(responses.Dequeue());
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        IpcDaemonLogsReadRequest Query,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
