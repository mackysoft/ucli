using MackySoft.Ucli.Application.Features.Eval;

namespace MackySoft.Tests;

internal sealed class RecordingEvalService : IEvalService
{
    private readonly Func<Guid, EvalCommandInput, CancellationToken, ValueTask<EvalServiceResult>> handler;

    public RecordingEvalService (Func<Guid, EvalCommandInput, CancellationToken, ValueTask<EvalServiceResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public List<Invocation> Invocations { get; } = [];

    public ValueTask<EvalServiceResult> ExecuteAsync (Guid requestId, EvalCommandInput input, CancellationToken cancellationToken = default)
    {
        Invocations.Add(new Invocation(requestId, input, cancellationToken));
        return handler(requestId, input, cancellationToken);
    }

    internal sealed record Invocation (Guid RequestId, EvalCommandInput Input, CancellationToken CancellationToken);
}
