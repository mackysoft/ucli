using MackySoft.Ucli.Hosting.Cli.Requests.Call.Preflight;

namespace MackySoft.Tests;

internal sealed class RecordingCallCommandPreflightService : ICallCommandPreflightService
{
    private readonly Func<string?, string, CancellationToken, ValueTask<CallCommandPreflightResult>> handler;
    private readonly List<Invocation> invocations = [];

    public RecordingCallCommandPreflightService (
        Func<string?, string, CancellationToken, ValueTask<CallCommandPreflightResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<CallCommandPreflightResult> PrepareAsync (
        Guid requestId,
        string? projectPath,
        string requestJson,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(requestId, projectPath, requestJson, cancellationToken));
        return handler(projectPath, requestJson, cancellationToken);
    }

    public readonly record struct Invocation (
        Guid RequestId,
        string? ProjectPath,
        string RequestJson,
        CancellationToken CancellationToken);
}
