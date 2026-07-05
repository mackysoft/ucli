using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.TestSupport;

internal sealed class StubModeDecisionService : IUnityExecutionModeDecisionService
{
    private readonly UnityExecutionModeDecisionResult result;
    private readonly List<Invocation> invocations = [];

    public StubModeDecisionService (UnityExecutionModeDecisionResult result)
    {
        this.result = result;
    }

    public Action<Invocation>? OnDecide { get; init; }

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityExecutionModeDecisionResult> DecideAsync (
        UnityExecutionMode mode,
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invocation = new Invocation(
            mode,
            unityProject,
            timeout,
            cancellationToken,
            TimeProvider);
        invocations.Add(invocation);
        OnDecide?.Invoke(invocation);
        return ValueTask.FromResult(result);
    }

    internal sealed record Invocation (
        UnityExecutionMode Mode,
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        CancellationToken CancellationToken,
        TimeProvider TimeProvider);
}
