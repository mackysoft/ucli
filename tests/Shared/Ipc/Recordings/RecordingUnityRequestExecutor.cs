using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingUnityRequestExecutor : IUnityRequestExecutor, IUnityStreamingRequestExecutor
{
    private readonly Queue<UnityRequestExecutionResult>? results;
    private readonly Func<UnityRequestPayload, UnityRequestExecutionResult>? resultFactory;
    private readonly Func<UnityRequestPayload, IReadOnlyList<UnityRequestProgressFrame>>? streamingProgressFramesFactory;

    private readonly List<Invocation> invocations = [];
    private readonly List<Invocation> streamingInvocations = [];

    public RecordingUnityRequestExecutor (params UnityRequestExecutionResult[] results)
    {
        if (results.Length == 0)
        {
            throw new ArgumentException("At least one result is required.", nameof(results));
        }

        this.results = new Queue<UnityRequestExecutionResult>(results);
    }

    public RecordingUnityRequestExecutor (
        Func<UnityRequestPayload, UnityRequestExecutionResult> resultFactory,
        Func<UnityRequestPayload, IReadOnlyList<UnityRequestProgressFrame>>? streamingProgressFramesFactory = null)
    {
        this.resultFactory = resultFactory;
        this.streamingProgressFramesFactory = streamingProgressFramesFactory;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public IReadOnlyList<Invocation> StreamingInvocations => streamingInvocations;

    public Action<InvocationContext>? OnExecute { get; init; }

    public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invocation = RecordInvocation(command, mode, timeout, config, unityProject, payload, cancellationToken);
        var result = CreateResult(invocation);
        return ValueTask.FromResult(result);
    }

    public async ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        Func<UnityRequestProgressFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invocation = RecordInvocation(command, mode, timeout, config, unityProject, payload, cancellationToken);
        streamingInvocations.Add(invocation);

        var progressFrames = streamingProgressFramesFactory?.Invoke(payload) ?? Array.Empty<UnityRequestProgressFrame>();
        for (var i = 0; i < progressFrames.Count; i++)
        {
            await onProgressFrame(progressFrames[i], cancellationToken).ConfigureAwait(false);
        }

        return CreateResult(invocation);
    }

    private Invocation RecordInvocation (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        CancellationToken cancellationToken)
    {
        var invocation = new Invocation(command, mode, timeout, config, unityProject, payload, cancellationToken);
        invocations.Add(invocation);
        OnExecute?.Invoke(new InvocationContext(invocations.Count, invocation));
        return invocation;
    }

    private UnityRequestExecutionResult CreateResult (Invocation invocation)
    {
        if (resultFactory is not null)
        {
            return resultFactory(invocation.Payload);
        }

        return results!.Count == 1 ? results.Peek() : results.Dequeue();
    }

    internal readonly record struct Invocation (
        UcliCommand Command,
        UnityExecutionMode Mode,
        TimeSpan Timeout,
        UcliConfig Config,
        ResolvedUnityProjectContext UnityProject,
        UnityRequestPayload Payload,
        CancellationToken CancellationToken);

    internal readonly record struct InvocationContext (
        int Index,
        Invocation Invocation);
}
