using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;

namespace MackySoft.Tests;

internal sealed class StubDaemonStopService : IDaemonStopService
{
    private readonly List<Invocation> invocations = [];

    public StubDaemonStopService (DaemonStopExecutionResult result)
    {
        Result = result;
    }

    public DaemonStopExecutionResult Result { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStopExecutionResult> StopAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(projectPath, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        string? ProjectPath,
        int? TimeoutMilliseconds,
        CancellationToken CancellationToken);
}
