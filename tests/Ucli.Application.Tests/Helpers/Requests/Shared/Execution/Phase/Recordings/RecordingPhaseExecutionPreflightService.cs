using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingPhaseExecutionPreflightService : IPhaseExecutionPreflightService
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public PhaseExecutionPreflightResult? Result { get; set; }

    public Action<ExecutionDeadline>? OnPrepare { get; init; }

    public ValueTask<PhaseExecutionPreflightResult> PrepareAsync (
        PreparedRequestContext preparedRequest,
        UnityExecutionMode mode,
        ExecutionDeadline deadline,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(preparedRequest);
        invocations.Add(new Invocation(
            preparedRequest,
            mode,
            deadline,
            failFast,
            cancellationToken));
        OnPrepare?.Invoke(deadline);

        if (Result is null)
        {
            throw new InvalidOperationException("Phase execution preflight result is not configured.");
        }

        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        PreparedRequestContext PreparedRequest,
        UnityExecutionMode Mode,
        ExecutionDeadline Deadline,
        bool FailFast,
        CancellationToken CancellationToken);
}
