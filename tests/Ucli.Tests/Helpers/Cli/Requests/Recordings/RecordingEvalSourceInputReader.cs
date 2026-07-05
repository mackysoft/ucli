using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;

namespace MackySoft.Tests;

internal sealed class RecordingEvalSourceInputReader : IEvalSourceInputReader
{
    private readonly Func<string?, string?, CancellationToken, ValueTask<EvalSourceInputReadResult>> handler;

    private readonly List<Invocation> invocations = [];

    public RecordingEvalSourceInputReader (
        Func<string?, string?, CancellationToken, ValueTask<EvalSourceInputReadResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<EvalSourceInputReadResult> ReadAsync (
        string? source,
        string? file,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(source, file, cancellationToken));
        return handler(source, file, cancellationToken);
    }

    public readonly record struct Invocation (
        string? Source,
        string? File,
        CancellationToken CancellationToken);
}
