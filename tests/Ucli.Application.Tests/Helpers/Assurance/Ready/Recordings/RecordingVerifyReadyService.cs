using MackySoft.Ucli.Application.Features.Assurance.Ready;

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

    internal readonly record struct Invocation (
        ReadyCommandInput Input,
        CancellationToken CancellationToken);
}
