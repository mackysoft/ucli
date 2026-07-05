using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class RecordingUnityIpcClient : IUnityIpcClient
{
    private readonly Queue<UnityRequestExecutionResult> results = [];

    private readonly List<Invocation> invocations = [];

    private readonly List<Invocation> streamingInvocations = [];

    public RecordingUnityIpcClient (params UnityRequestExecutionResult[] results)
        : this(UnityExecutionTarget.Daemon, results)
    {
    }

    public RecordingUnityIpcClient (
        UnityExecutionTarget target,
        params UnityRequestExecutionResult[] results)
    {
        Target = target;
        foreach (var result in results)
        {
            this.results.Enqueue(result);
        }
    }

    public UnityExecutionTarget Target { get; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public IReadOnlyList<Invocation> StreamingInvocations => streamingInvocations;

    public ValueTask<UnityRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invocation = new Invocation(unityProject, dispatchRequest, timeout, cancellationToken);
        invocations.Add(invocation);
        return ValueTask.FromResult(DequeueResult());
    }

    public ValueTask<UnityRequestExecutionResult> SendStreamingAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        cancellationToken.ThrowIfCancellationRequested();
        var invocation = new Invocation(unityProject, dispatchRequest, timeout, cancellationToken);
        invocations.Add(invocation);
        streamingInvocations.Add(invocation);
        return ValueTask.FromResult(DequeueResult());
    }

    private UnityRequestExecutionResult DequeueResult ()
    {
        if (results.Count == 0)
        {
            throw new Xunit.Sdk.XunitException("No Unity IPC client result was configured.");
        }

        return results.Dequeue();
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        UnityIpcDispatchRequest DispatchRequest,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
