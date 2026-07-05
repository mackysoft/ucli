using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingUnityLogsClient : IUnityLogsClient
{
    private readonly Queue<UnityLogsClientReadResult> responses;
    private readonly List<Invocation> invocations = [];

    public RecordingUnityLogsClient (IEnumerable<UnityLogsClientReadResult> responses)
    {
        this.responses = new Queue<UnityLogsClientReadResult>(responses);
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityLogsClientReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        IpcUnityLogsReadRequest query,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(unityProject, query, timeout, cancellationToken));

        if (responses.Count == 0)
        {
            return ValueTask.FromResult(UnityLogsClientReadResult.Success(
                new IpcUnityLogsReadResponse(Array.Empty<IpcUnityLogEvent>(), query.After ?? "stream-1:1")));
        }

        return ValueTask.FromResult(responses.Dequeue());
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        IpcUnityLogsReadRequest Query,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
