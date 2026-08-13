using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingVerifyReadyService : IReadyService
{
    private readonly Func<ReadyCommandInput, ReadyExecutionResult> resultFactory;
    private readonly List<Invocation> invocations = [];

    public RecordingVerifyReadyService (Func<ReadyCommandInput, ReadyExecutionResult> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<ReadyExecutionResult> ExecuteAsync (
        ReadyCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(input, cancellationToken));
        return ValueTask.FromResult(resultFactory(input));
    }

    public ValueTask<ProgramReadyObservation> ObserveOnFixedHostAsync (
        ProjectContext context,
        IUnityExecutionHostBinding binding,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    internal readonly record struct Invocation (
        ReadyCommandInput Input,
        CancellationToken CancellationToken);
}
